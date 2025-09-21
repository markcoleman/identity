namespace IdentityResolution.Core.Models;

/// <summary>
/// Result of an identity resolution operation
/// </summary>
public class ResolutionResult
{
    /// <summary>
    /// The Enterprise Person ID (EPID) - a stable identifier for the resolved person
    /// </summary>
    public string EPID { get; set; } = string.Empty;

    /// <summary>
    /// The resolved identity (may be merged)
    /// </summary>
    public Identity ResolvedIdentity { get; set; } = null!;

    /// <summary>
    /// All matches found during resolution
    /// </summary>
    public List<IdentityMatch> Matches { get; set; } = new();

    /// <summary>
    /// The resolution decision made
    /// </summary>
    public ResolutionDecision Decision { get; set; }

    /// <summary>
    /// Whether any identities were automatically merged
    /// </summary>
    public bool WasAutoMerged { get; set; }

    /// <summary>
    /// Identities that were merged (if any)
    /// </summary>
    public List<Identity> MergedIdentities { get; set; } = new();

    /// <summary>
    /// Processing time for the resolution
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Any warnings or notes about the resolution
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// The resolution strategy used
    /// </summary>
    public string? Strategy { get; set; }

    /// <summary>
    /// Detailed explanation of how the resolution decision was made
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Audit information for governance
    /// </summary>
    public Dictionary<string, object> AuditData { get; set; } = new();

    /// <summary>
    /// Unique identifier for this resolution attempt
    /// </summary>
    public Guid ResolutionId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// When this resolution was performed
    /// </summary>
    public DateTime ResolvedAt { get; set; } = DateTime.UtcNow;
}
