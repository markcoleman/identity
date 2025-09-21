using System;
using System.Collections.Generic;

namespace IdentityResolution.Core.Models;

/// <summary>
/// Immutable audit record for identity matching and resolution operations
/// </summary>
public record AuditRecord
{
    /// <summary>
    /// Unique identifier for this audit record
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Timestamp when this operation occurred
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Type of operation (Match, Resolve, Merge, Split)
    /// </summary>
    public AuditOperationType OperationType { get; init; }

    /// <summary>
    /// Identity ID that was the subject of the operation
    /// </summary>
    public Guid? SourceIdentityId { get; init; }

    /// <summary>
    /// User or system that performed the operation
    /// </summary>
    public string Actor { get; init; } = string.Empty;

    /// <summary>
    /// System or source that initiated the operation
    /// </summary>
    public string? SourceSystem { get; init; }

    /// <summary>
    /// Input data provided for the operation
    /// </summary>
    public Dictionary<string, object> Inputs { get; init; } = new();

    /// <summary>
    /// Feature scores and matching details
    /// </summary>
    public Dictionary<string, object> Features { get; init; } = new();

    /// <summary>
    /// Final confidence score
    /// </summary>
    public double? Score { get; init; }

    /// <summary>
    /// Decision made (Auto, Review, New)
    /// </summary>
    public ResolutionDecision? Decision { get; init; }

    /// <summary>
    /// Matching algorithm used
    /// </summary>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Configuration used during the operation
    /// </summary>
    public Dictionary<string, object> Configuration { get; init; } = new();

    /// <summary>
    /// Result of the operation
    /// </summary>
    public Dictionary<string, object> Result { get; init; } = new();

    /// <summary>
    /// Processing time for the operation
    /// </summary>
    public TimeSpan? ProcessingTime { get; init; }

    /// <summary>
    /// Error information if the operation failed
    /// </summary>
    public string? ErrorDetails { get; init; }

    /// <summary>
    /// Session or correlation ID for tracking related operations
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Types of audit operations
/// </summary>
public enum AuditOperationType
{
    /// <summary>
    /// Identity matching operation
    /// </summary>
    Match,

    /// <summary>
    /// Identity resolution operation
    /// </summary>
    Resolve,

    /// <summary>
    /// Identity merge operation
    /// </summary>
    Merge,

    /// <summary>
    /// Identity split operation
    /// </summary>
    Split,

    /// <summary>
    /// Review queue decision
    /// </summary>
    Review,

    /// <summary>
    /// Identity creation
    /// </summary>
    Create,

    /// <summary>
    /// Identity update
    /// </summary>
    Update,

    /// <summary>
    /// Identity deletion
    /// </summary>
    Delete
}

/// <summary>
/// Represents the history of merges and splits for an identity
/// </summary>
public class IdentityLineage
{
    /// <summary>
    /// The current identity ID
    /// </summary>
    public Guid IdentityId { get; set; }

    /// <summary>
    /// All merge events involving this identity
    /// </summary>
    public List<MergeEvent> MergeEvents { get; set; } = new();

    /// <summary>
    /// All split events involving this identity
    /// </summary>
    public List<SplitEvent> SplitEvents { get; set; } = new();

    /// <summary>
    /// When this lineage record was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this lineage record was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a merge event between identities
/// </summary>
public class MergeEvent
{
    /// <summary>
    /// Unique identifier for this merge event
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The primary identity that was kept
    /// </summary>
    public Guid PrimaryIdentityId { get; set; }

    /// <summary>
    /// The secondary identity that was merged in
    /// </summary>
    public Guid SecondaryIdentityId { get; set; }

    /// <summary>
    /// The resulting identity after merge
    /// </summary>
    public Guid ResultingIdentityId { get; set; }

    /// <summary>
    /// When the merge occurred
    /// </summary>
    public DateTime MergedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who or what performed the merge
    /// </summary>
    public string MergedBy { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score that triggered the merge
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Whether this was an automatic or manual merge
    /// </summary>
    public bool IsAutomatic { get; set; }

    /// <summary>
    /// Reason for the merge
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Additional context about the merge
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Represents a split event where an identity was separated
/// </summary>
public class SplitEvent
{
    /// <summary>
    /// Unique identifier for this split event
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The original identity that was split
    /// </summary>
    public Guid OriginalIdentityId { get; set; }

    /// <summary>
    /// The resulting identities after split
    /// </summary>
    public List<Guid> ResultingIdentityIds { get; set; } = new();

    /// <summary>
    /// When the split occurred
    /// </summary>
    public DateTime SplitAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who performed the split
    /// </summary>
    public string SplitBy { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the split
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Additional context about the split
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
}
