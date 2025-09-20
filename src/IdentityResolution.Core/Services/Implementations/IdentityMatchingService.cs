using System.Diagnostics;
using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.Extensions.Logging;

namespace IdentityResolution.Core.Services.Implementations;

/// <summary>
/// Implementation of identity matching service with deterministic and probabilistic algorithms
/// </summary>
public class IdentityMatchingService : IIdentityMatchingService
{
    private readonly IIdentityStorageService _storageService;
    private readonly ITokenizationService _tokenizationService;
    private readonly ILogger<IdentityMatchingService> _logger;

    public IdentityMatchingService(
        IIdentityStorageService storageService,
        ITokenizationService tokenizationService,
        ILogger<IdentityMatchingService> logger)
    {
        _storageService = storageService;
        _tokenizationService = tokenizationService;
        _logger = logger;
    }

    /// <summary>
    /// Find potential matches for a given identity using deterministic and probabilistic matching
    /// </summary>
    public async Task<MatchResult> FindMatchesAsync(Identity identity, MatchingConfiguration? configuration = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        configuration ??= new MatchingConfiguration();

        _logger.LogInformation("Starting match search for identity {IdentityId}", identity.Id);

        var candidates = await _storageService.GetAllIdentitiesAsync(cancellationToken);
        var candidatesList = candidates.Where(c => c.Id != identity.Id).ToList();

        var matches = new List<IdentityMatch>();

        foreach (var candidate in candidatesList)
        {
            var match = CompareIdentities(identity, candidate, configuration);
            
            // Only include matches above the minimum threshold
            if (match.OverallScore >= configuration.MinimumMatchThreshold)
            {
                matches.Add(match);
            }
        }

        // Sort by score descending
        matches.Sort((a, b) => b.OverallScore.CompareTo(a.OverallScore));

        // Limit results
        if (matches.Count > configuration.MaxResults)
        {
            matches = matches.Take(configuration.MaxResults).ToList();
        }

        stopwatch.Stop();

        var result = new MatchResult
        {
            SourceIdentity = identity,
            Matches = matches,
            ProcessingTime = stopwatch.Elapsed,
            CandidatesEvaluated = candidatesList.Count,
            Algorithm = "Deterministic + Probabilistic"
        };

        _logger.LogInformation("Found {MatchCount} potential matches for identity {IdentityId} in {ElapsedMs}ms",
            matches.Count, identity.Id, stopwatch.ElapsedMilliseconds);

        return result;
    }

    /// <summary>
    /// Compare two identities using both deterministic and probabilistic methods
    /// </summary>
    public IdentityMatch CompareIdentities(Identity identity1, Identity identity2, MatchingConfiguration? configuration = null)
    {
        configuration ??= new MatchingConfiguration();

        var match = new IdentityMatch
        {
            SourceIdentity = identity1,
            CandidateIdentity = identity2,
            Algorithm = "Hybrid Deterministic + Probabilistic"
        };

        // First, try deterministic matching
        var deterministicScore = CalculateDeterministicScore(identity1, identity2, match);
        
        if (deterministicScore >= 1.0)
        {
            // Perfect deterministic match
            match.OverallScore = 1.0;
            match.MatchReasons.Add("Deterministic match on verified identifiers");
            match.IsAutoMergeCandidate = true;
            return match;
        }

        // If there's a conflict in deterministic identifiers, flag for review
        if (HasDeterministicConflict(identity1, identity2, match))
        {
            match.OverallScore = 0.0;
            match.MatchReasons.Add("Deterministic conflict detected - requires manual review");
            match.Status = MatchStatus.RequiresReview;
            return match;
        }

        // Fall back to probabilistic matching
        var probabilisticScore = CalculateProbabilisticScore(identity1, identity2, match, configuration);
        match.OverallScore = Math.Max(deterministicScore, probabilisticScore);

        // Set auto-merge candidate flag
        match.IsAutoMergeCandidate = match.OverallScore >= configuration.AutoMergeThreshold;

        // Set status based on thresholds
        if (match.OverallScore >= configuration.AutoMergeThreshold)
        {
            match.Status = MatchStatus.Pending; // Will be auto-merged
        }
        else if (match.OverallScore >= configuration.ReviewThreshold)
        {
            match.Status = MatchStatus.RequiresReview;
        }
        else
        {
            match.Status = MatchStatus.Rejected;
        }

        return match;
    }

    /// <summary>
    /// Calculate deterministic score based on exact matches of verified identifiers
    /// </summary>
    private double CalculateDeterministicScore(Identity identity1, Identity identity2, IdentityMatch match)
    {
        var score = 0.0;
        var totalChecks = 0;

        // Check SSN + DOB combination (highest priority)
        var ssn1 = GetSSNIdentifier(identity1);
        var ssn2 = GetSSNIdentifier(identity2);
        
        if (!string.IsNullOrEmpty(ssn1) && !string.IsNullOrEmpty(ssn2))
        {
            totalChecks++;
            var token1 = _tokenizationService.TokenizeSSN(ssn1);
            var token2 = _tokenizationService.TokenizeSSN(ssn2);
            
            if (string.Equals(token1, token2, StringComparison.Ordinal))
            {
                // SSN matches, now check DOB
                if (identity1.PersonalInfo.DateOfBirth.HasValue && 
                    identity2.PersonalInfo.DateOfBirth.HasValue &&
                    identity1.PersonalInfo.DateOfBirth.Value.Date == identity2.PersonalInfo.DateOfBirth.Value.Date)
                {
                    score = 1.0; // Perfect deterministic match
                    match.MatchReasons.Add("Exact SSN + DOB match");
                    match.FieldScores["SSN"] = 1.0;
                    match.FieldScores["DateOfBirth"] = 1.0;
                    return score;
                }
                else
                {
                    match.MatchReasons.Add("SSN matches but DOB differs - flagging for review");
                    match.FieldScores["SSN"] = 1.0;
                    match.FieldScores["DateOfBirth"] = 0.0;
                    return -1.0; // Indicates conflict
                }
            }
        }

        // Check other exact match identifiers
        foreach (var field in new[] { "DriversLicense", "Passport" })
        {
            var value1 = GetIdentifierValue(identity1, field);
            var value2 = GetIdentifierValue(identity2, field);
            
            if (!string.IsNullOrEmpty(value1) && !string.IsNullOrEmpty(value2))
            {
                totalChecks++;
                if (string.Equals(value1, value2, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.8; // High score but not perfect like SSN+DOB
                    match.MatchReasons.Add($"Exact {field} match");
                    match.FieldScores[field] = 1.0;
                }
                else
                {
                    match.FieldScores[field] = 0.0;
                }
            }
        }

        return totalChecks > 0 ? Math.Min(score / totalChecks, 1.0) : 0.0;
    }

    /// <summary>
    /// Check for conflicts in deterministic identifiers
    /// </summary>
    private bool HasDeterministicConflict(Identity identity1, Identity identity2, IdentityMatch match)
    {
        // Check if SSN matches but DOB differs (already handled above but kept for clarity)
        var ssn1 = GetSSNIdentifier(identity1);
        var ssn2 = GetSSNIdentifier(identity2);
        
        if (!string.IsNullOrEmpty(ssn1) && !string.IsNullOrEmpty(ssn2))
        {
            var token1 = _tokenizationService.TokenizeSSN(ssn1);
            var token2 = _tokenizationService.TokenizeSSN(ssn2);
            
            if (string.Equals(token1, token2, StringComparison.Ordinal))
            {
                if (identity1.PersonalInfo.DateOfBirth.HasValue && 
                    identity2.PersonalInfo.DateOfBirth.HasValue &&
                    identity1.PersonalInfo.DateOfBirth.Value.Date != identity2.PersonalInfo.DateOfBirth.Value.Date)
                {
                    return true; // SSN matches but DOB differs - conflict
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Calculate probabilistic score using weighted field comparisons
    /// </summary>
    private double CalculateProbabilisticScore(Identity identity1, Identity identity2, IdentityMatch match, MatchingConfiguration configuration)
    {
        var weightedScore = 0.0;
        var totalWeight = 0.0;

        // Compare names
        var firstNameScore = CompareNames(identity1.PersonalInfo.FirstName, identity2.PersonalInfo.FirstName, configuration.EnableFuzzyMatching);
        var lastNameScore = CompareNames(identity1.PersonalInfo.LastName, identity2.PersonalInfo.LastName, configuration.EnableFuzzyMatching);
        
        AddFieldScore("FirstName", firstNameScore, configuration.FieldWeights, ref weightedScore, ref totalWeight, match);
        AddFieldScore("LastName", lastNameScore, configuration.FieldWeights, ref weightedScore, ref totalWeight, match);

        // Compare email
        var emailScore = CompareEmails(identity1.ContactInfo.Email, identity2.ContactInfo.Email);
        AddFieldScore("Email", emailScore, configuration.FieldWeights, ref weightedScore, ref totalWeight, match);

        // Compare phone
        var phoneScore = ComparePhones(identity1.ContactInfo.Phone, identity2.ContactInfo.Phone);
        AddFieldScore("Phone", phoneScore, configuration.FieldWeights, ref weightedScore, ref totalWeight, match);

        // Compare date of birth
        var dobScore = CompareDateOfBirth(identity1.PersonalInfo.DateOfBirth, identity2.PersonalInfo.DateOfBirth);
        AddFieldScore("DateOfBirth", dobScore, configuration.FieldWeights, ref weightedScore, ref totalWeight, match);

        var finalScore = totalWeight > 0 ? weightedScore / totalWeight : 0.0;

        // Add explanation
        match.MatchReasons.Add($"Probabilistic matching score: {finalScore:F3}");
        
        return finalScore;
    }

    private void AddFieldScore(string field, double score, Dictionary<string, double> weights, ref double weightedScore, ref double totalWeight, IdentityMatch match)
    {
        if (weights.TryGetValue(field, out var weight) && weight > 0)
        {
            weightedScore += score * weight;
            totalWeight += weight;
            match.FieldScores[field] = score;
            
            if (score > 0.7)
            {
                match.MatchReasons.Add($"{field} similarity: {score:F3}");
            }
        }
    }

    private double CompareNames(string? name1, string? name2, bool enableFuzzy)
    {
        if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
            return 0.0;

        name1 = name1.Trim().ToUpperInvariant();
        name2 = name2.Trim().ToUpperInvariant();

        if (name1 == name2)
            return 1.0;

        if (!enableFuzzy)
            return 0.0;

        // Simple fuzzy matching using Levenshtein distance
        return 1.0 - (double)LevenshteinDistance(name1, name2) / Math.Max(name1.Length, name2.Length);
    }

    private double CompareEmails(string? email1, string? email2)
    {
        if (string.IsNullOrWhiteSpace(email1) || string.IsNullOrWhiteSpace(email2))
            return 0.0;

        return string.Equals(email1.Trim(), email2.Trim(), StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
    }

    private double ComparePhones(string? phone1, string? phone2)
    {
        if (string.IsNullOrWhiteSpace(phone1) || string.IsNullOrWhiteSpace(phone2))
            return 0.0;

        // Normalize phones by removing non-digits
        var clean1 = System.Text.RegularExpressions.Regex.Replace(phone1, @"[^\d]", "");
        var clean2 = System.Text.RegularExpressions.Regex.Replace(phone2, @"[^\d]", "");

        return string.Equals(clean1, clean2, StringComparison.Ordinal) ? 1.0 : 0.0;
    }

    private double CompareDateOfBirth(DateTime? dob1, DateTime? dob2)
    {
        if (!dob1.HasValue || !dob2.HasValue)
            return 0.0;

        return dob1.Value.Date == dob2.Value.Date ? 1.0 : 0.0;
    }

    private string? GetSSNIdentifier(Identity identity)
    {
        return identity.Identifiers
            .FirstOrDefault(i => string.Equals(i.Type, IdentifierTypes.SocialSecurityNumber, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private string? GetIdentifierValue(Identity identity, string type)
    {
        return identity.Identifiers
            .FirstOrDefault(i => string.Equals(i.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private int LevenshteinDistance(string s1, string s2)
    {
        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }
}