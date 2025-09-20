namespace IdentityResolution.Core.Models;

/// <summary>
/// Result of an identity resolution operation
/// </summary>
public class ResolutionResult
{
    /// <summary>
    /// The resolved identity (may be merged)
    /// </summary>
    public Identity ResolvedIdentity { get; set; } = null!;
    
    /// <summary>
    /// All matches found during resolution
    /// </summary>
    public List<IdentityMatch> Matches { get; set; } = new();
    
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
}