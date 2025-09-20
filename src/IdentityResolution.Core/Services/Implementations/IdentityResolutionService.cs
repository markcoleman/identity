using System.Diagnostics;
using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.Extensions.Logging;

namespace IdentityResolution.Core.Services.Implementations;

/// <summary>
/// Implementation of identity resolution service that orchestrates matching and decision logic
/// </summary>
public class IdentityResolutionService : IIdentityResolutionService
{
    private readonly IIdentityMatchingService _matchingService;
    private readonly IIdentityStorageService _storageService;
    private readonly IDataNormalizationService _normalizationService;
    private readonly ILogger<IdentityResolutionService> _logger;

    public IdentityResolutionService(
        IIdentityMatchingService matchingService,
        IIdentityStorageService storageService,
        IDataNormalizationService normalizationService,
        ILogger<IdentityResolutionService> logger)
    {
        _matchingService = matchingService;
        _storageService = storageService;
        _normalizationService = normalizationService;
        _logger = logger;
    }

    /// <summary>
    /// Resolve an identity by finding matches and making decisions based on thresholds
    /// </summary>
    public async Task<ResolutionResult> ResolveIdentityAsync(Identity identity, MatchingConfiguration? configuration = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        configuration ??= new MatchingConfiguration();

        _logger.LogInformation("Starting identity resolution for {IdentityId}", identity.Id);

        var result = new ResolutionResult
        {
            Strategy = "Deterministic + Probabilistic with Thresholding"
        };

        try
        {
            // Normalize the input identity
            var normalizedIdentity = _normalizationService.NormalizeIdentity(identity);

            // Find potential matches
            var matchResult = await _matchingService.FindMatchesAsync(normalizedIdentity, configuration, cancellationToken);
            result.Matches = matchResult.Matches;

            // Make resolution decision based on matches and thresholds
            var decision = MakeResolutionDecision(matchResult, configuration, result);
            result.Decision = decision;

            // Execute the decision
            await ExecuteResolutionDecision(decision, normalizedIdentity, matchResult, result, cancellationToken);

            // Add audit information
            PopulateAuditData(result, matchResult, configuration);

            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;

            _logger.LogInformation("Identity resolution completed for {IdentityId} with decision {Decision} in {ElapsedMs}ms",
                identity.Id, decision, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during identity resolution for {IdentityId}", identity.Id);
            
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            result.Decision = ResolutionDecision.Review;
            result.Warnings.Add($"Resolution failed due to error: {ex.Message}");
            result.ResolvedIdentity = identity;
            
            return result;
        }
    }

    /// <summary>
    /// Merge two identities into one, keeping the primary identity as the base
    /// </summary>
    public Identity MergeIdentities(Identity primaryIdentity, Identity secondaryIdentity)
    {
        _logger.LogInformation("Merging identity {SecondaryId} into {PrimaryId}", 
            secondaryIdentity.Id, primaryIdentity.Id);

        var merged = new Identity
        {
            Id = primaryIdentity.Id,
            CreatedAt = primaryIdentity.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            Source = primaryIdentity.Source,
            Confidence = Math.Max(primaryIdentity.Confidence, secondaryIdentity.Confidence)
        };

        // Merge personal information - prefer non-null/non-empty values
        merged.PersonalInfo = new PersonalInfo
        {
            FirstName = ChooseBestValue(primaryIdentity.PersonalInfo.FirstName, secondaryIdentity.PersonalInfo.FirstName),
            MiddleName = ChooseBestValue(primaryIdentity.PersonalInfo.MiddleName, secondaryIdentity.PersonalInfo.MiddleName),
            LastName = ChooseBestValue(primaryIdentity.PersonalInfo.LastName, secondaryIdentity.PersonalInfo.LastName),
            FullName = ChooseBestValue(primaryIdentity.PersonalInfo.FullName, secondaryIdentity.PersonalInfo.FullName),
            DateOfBirth = primaryIdentity.PersonalInfo.DateOfBirth ?? secondaryIdentity.PersonalInfo.DateOfBirth,
            Gender = ChooseBestValue(primaryIdentity.PersonalInfo.Gender, secondaryIdentity.PersonalInfo.Gender),
            Address = MergeAddresses(primaryIdentity.PersonalInfo.Address, secondaryIdentity.PersonalInfo.Address)
        };

        // Merge contact information
        merged.ContactInfo = new ContactInfo
        {
            Email = ChooseBestValue(primaryIdentity.ContactInfo.Email, secondaryIdentity.ContactInfo.Email),
            Phone = ChooseBestValue(primaryIdentity.ContactInfo.Phone, secondaryIdentity.ContactInfo.Phone),
            AlternatePhone = ChooseBestValue(primaryIdentity.ContactInfo.AlternatePhone, secondaryIdentity.ContactInfo.AlternatePhone),
            Website = ChooseBestValue(primaryIdentity.ContactInfo.Website, secondaryIdentity.ContactInfo.Website)
        };

        // Merge identifiers - combine unique identifiers
        merged.Identifiers = MergeIdentifiers(primaryIdentity.Identifiers, secondaryIdentity.Identifiers);

        // Merge attributes
        merged.Attributes = MergeAttributes(primaryIdentity.Attributes, secondaryIdentity.Attributes);

        return merged;
    }

    /// <summary>
    /// Make resolution decision based on match results and configuration thresholds
    /// </summary>
    private ResolutionDecision MakeResolutionDecision(MatchResult matchResult, MatchingConfiguration configuration, ResolutionResult result)
    {
        if (!matchResult.Matches.Any())
        {
            result.Explanation = "No potential matches found. Creating new identity record.";
            return ResolutionDecision.New;
        }

        var bestMatch = matchResult.Matches.First();

        // Check for conflicts (negative scores indicate deterministic conflicts)
        if (bestMatch.OverallScore < 0)
        {
            result.Explanation = "Deterministic conflict detected in verified identifiers. Manual review required.";
            return ResolutionDecision.Review;
        }

        // Check for automatic merge threshold (≥ 0.97)
        if (bestMatch.OverallScore >= configuration.AutoMergeThreshold)
        {
            result.Explanation = $"High confidence match found (score: {bestMatch.OverallScore:F3}). Automatically resolving to existing identity.";
            return ResolutionDecision.Auto;
        }

        // Check for review threshold (0.90 - 0.97)
        if (bestMatch.OverallScore >= configuration.ReviewThreshold)
        {
            result.Explanation = $"Medium confidence match found (score: {bestMatch.OverallScore:F3}). Manual review recommended.";
            return ResolutionDecision.Review;
        }

        // Below review threshold (< 0.90) - create new
        result.Explanation = $"Low confidence matches only (best score: {bestMatch.OverallScore:F3}). Creating new identity record.";
        return ResolutionDecision.New;
    }

    /// <summary>
    /// Execute the resolution decision
    /// </summary>
    private async Task ExecuteResolutionDecision(
        ResolutionDecision decision, 
        Identity normalizedIdentity, 
        MatchResult matchResult, 
        ResolutionResult result, 
        CancellationToken cancellationToken)
    {
        switch (decision)
        {
            case ResolutionDecision.Auto:
                // Merge with best match
                var bestMatch = matchResult.Matches.First();
                var mergedIdentity = MergeIdentities(bestMatch.CandidateIdentity, normalizedIdentity);
                result.ResolvedIdentity = await _storageService.UpdateIdentityAsync(mergedIdentity, cancellationToken);
                result.WasAutoMerged = true;
                result.MergedIdentities.Add(normalizedIdentity);
                break;

            case ResolutionDecision.Review:
                // Store identity but flag for review
                result.ResolvedIdentity = await _storageService.StoreIdentityAsync(normalizedIdentity, cancellationToken);
                result.Warnings.Add("Identity requires manual review before final resolution");
                // In a real system, this would be queued for review
                break;

            case ResolutionDecision.New:
                // Create new identity
                result.ResolvedIdentity = await _storageService.StoreIdentityAsync(normalizedIdentity, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Populate audit data for governance and compliance
    /// </summary>
    private void PopulateAuditData(ResolutionResult result, MatchResult matchResult, MatchingConfiguration configuration)
    {
        result.AuditData["algorithm"] = matchResult.Algorithm;
        result.AuditData["candidatesEvaluated"] = matchResult.CandidatesEvaluated;
        result.AuditData["matchCount"] = matchResult.Matches.Count;
        result.AuditData["thresholds"] = new
        {
            autoMerge = configuration.AutoMergeThreshold,
            review = configuration.ReviewThreshold,
            minimum = configuration.MinimumMatchThreshold
        };
        
        if (matchResult.Matches.Any())
        {
            var bestMatch = matchResult.Matches.First();
            result.AuditData["bestMatchScore"] = bestMatch.OverallScore;
            result.AuditData["bestMatchId"] = bestMatch.CandidateIdentity.Id;
            result.AuditData["fieldScores"] = bestMatch.FieldScores;
        }

        result.AuditData["processingTimeMs"] = result.ProcessingTime.TotalMilliseconds;
    }

    private string? ChooseBestValue(string? primary, string? secondary)
    {
        return !string.IsNullOrWhiteSpace(primary) ? primary :
               !string.IsNullOrWhiteSpace(secondary) ? secondary : null;
    }

    private Address? MergeAddresses(Address? primary, Address? secondary)
    {
        if (primary == null) return secondary;
        if (secondary == null) return primary;

        return new Address
        {
            Street1 = ChooseBestValue(primary.Street1, secondary.Street1),
            Street2 = ChooseBestValue(primary.Street2, secondary.Street2),
            City = ChooseBestValue(primary.City, secondary.City),
            State = ChooseBestValue(primary.State, secondary.State),
            PostalCode = ChooseBestValue(primary.PostalCode, secondary.PostalCode),
            Country = ChooseBestValue(primary.Country, secondary.Country)
        };
    }

    private List<Identifier> MergeIdentifiers(List<Identifier> primary, List<Identifier> secondary)
    {
        var merged = new List<Identifier>(primary);
        
        foreach (var secondaryId in secondary)
        {
            // Only add if type doesn't already exist
            if (!merged.Any(p => string.Equals(p.Type, secondaryId.Type, StringComparison.OrdinalIgnoreCase)))
            {
                merged.Add(secondaryId);
            }
        }

        return merged;
    }

    private Dictionary<string, string> MergeAttributes(Dictionary<string, string> primary, Dictionary<string, string> secondary)
    {
        var merged = new Dictionary<string, string>(primary);
        
        foreach (var kvp in secondary)
        {
            if (!merged.ContainsKey(kvp.Key))
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        return merged;
    }
}