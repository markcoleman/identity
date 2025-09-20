namespace IdentityResolution.Core.Models;

/// <summary>
/// Represents a potential match between two identities
/// </summary>
public class IdentityMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The source identity being matched
    /// </summary>
    public Identity SourceIdentity { get; set; } = null!;

    /// <summary>
    /// The candidate identity that might be a match
    /// </summary>
    public Identity CandidateIdentity { get; set; } = null!;

    /// <summary>
    /// Overall confidence score for this match (0.0 to 1.0)
    /// </summary>
    public double OverallScore { get; set; }

    /// <summary>
    /// Individual field match scores
    /// </summary>
    public Dictionary<string, double> FieldScores { get; set; } = new();

    /// <summary>
    /// Match reasons and explanations
    /// </summary>
    public List<string> MatchReasons { get; set; } = new();

    /// <summary>
    /// When this match was calculated
    /// </summary>
    public DateTime MatchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The matching algorithm or rule set used
    /// </summary>
    public string? Algorithm { get; set; }

    /// <summary>
    /// Whether this match exceeds the auto-merge threshold
    /// </summary>
    public bool IsAutoMergeCandidate { get; set; }

    /// <summary>
    /// Status of this match
    /// </summary>
    public MatchStatus Status { get; set; } = MatchStatus.Pending;
}

/// <summary>
/// Status of an identity match
/// </summary>
public enum MatchStatus
{
    Pending,
    Accepted,
    Rejected,
    Merged,
    RequiresReview
}

/// <summary>
/// Resolution decision types
/// </summary>
public enum ResolutionDecision
{
    /// <summary>
    /// Automatically merge/resolve identities
    /// </summary>
    Auto,

    /// <summary>
    /// Requires manual review before resolution
    /// </summary>
    Review,

    /// <summary>
    /// Create new identity record
    /// </summary>
    New
}

/// <summary>
/// Result of a batch matching operation
/// </summary>
public class MatchResult
{
    public Identity SourceIdentity { get; set; } = null!;
    public List<IdentityMatch> Matches { get; set; } = new();
    public TimeSpan ProcessingTime { get; set; }
    public int CandidatesEvaluated { get; set; }
    public string? Algorithm { get; set; }
}

/// <summary>
/// Configuration for matching operations
/// </summary>
public class MatchingConfiguration
{
    /// <summary>
    /// Minimum score required to consider a potential match
    /// </summary>
    public double MinimumMatchThreshold { get; set; } = 0.6;

    /// <summary>
    /// Score threshold for automatic merging
    /// </summary>
    public double AutoMergeThreshold { get; set; } = 0.97;

    /// <summary>
    /// Score threshold for requiring manual review
    /// </summary>
    public double ReviewThreshold { get; set; } = 0.90;

    /// <summary>
    /// Maximum number of matches to return
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Whether to enable fuzzy string matching
    /// </summary>
    public bool EnableFuzzyMatching { get; set; } = true;

    /// <summary>
    /// Field weights for scoring
    /// </summary>
    public Dictionary<string, double> FieldWeights { get; set; } = new()
    {
        ["FirstName"] = 0.2,
        ["LastName"] = 0.25,
        ["Email"] = 0.3,
        ["Phone"] = 0.15,
        ["DateOfBirth"] = 0.1
    };

    /// <summary>
    /// Fields that must match exactly (e.g., SSN)
    /// </summary>
    public HashSet<string> ExactMatchFields { get; set; } = new()
    {
        "SSN",
        "DriversLicense",
        "Passport"
    };
}
