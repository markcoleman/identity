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
    /// Input data provided for the operation (encrypted/tokenized)
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
    public Dictionary<string, object> Results { get; init; } = new();

    /// <summary>
    /// Processing time for the operation
    /// </summary>
    public TimeSpan? ProcessingTime { get; init; }

    /// <summary>
    /// Correlation ID for tracking related operations
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Version of the algorithm/system used
    /// </summary>
    public string? AlgorithmVersion { get; init; }

    /// <summary>
    /// Any errors or warnings during operation
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Types of audit operations
/// </summary>
public enum AuditOperationType
{
    Match,
    Resolve,
    Merge,
    Split,
    Create,
    Update,
    Delete,
    Replay
}

/// <summary>
/// Golden profile representing the consolidated identity
/// </summary>
public class GoldenProfile
{
    /// <summary>
    /// Unique person identifier
    /// </summary>
    public Guid PersonId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Enterprise Person ID (EPID) for external references
    /// </summary>
    public string? EPID { get; set; }

    /// <summary>
    /// Current status of the golden profile
    /// </summary>
    public GoldenProfileStatus Status { get; set; } = GoldenProfileStatus.Active;

    /// <summary>
    /// Verified identifiers for this person
    /// </summary>
    public List<Identifier> VerifiedIdentifiers { get; set; } = new();

    /// <summary>
    /// Consolidated attributes from all source identities
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();

    /// <summary>
    /// Current canonical identity representation
    /// </summary>
    public Identity? CanonicalIdentity { get; set; }

    /// <summary>
    /// List of source identity IDs that contributed to this profile
    /// </summary>
    public List<Guid> SourceIdentityIds { get; set; } = new();

    /// <summary>
    /// Confidence score of the golden profile
    /// </summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>
    /// When this golden profile was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this golden profile was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Version number for optimistic concurrency control
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Metadata for governance and lineage
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Status of a golden profile
/// </summary>
public enum GoldenProfileStatus
{
    Active,
    Inactive,
    Merged,
    Split,
    Suspended,
    Deleted
}

/// <summary>
/// Represents a merge event where identities were combined
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
    /// The resulting golden profile ID
    /// </summary>
    public Guid ResultingIdentityId { get; set; }

    /// <summary>
    /// The actor (user/system) who performed the merge
    /// </summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the merge
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Confidence score that triggered the merge
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// When the merge occurred
    /// </summary>
    public DateTime MergedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Algorithm version used for the merge decision
    /// </summary>
    public string? AlgorithmVersion { get; set; }

    /// <summary>
    /// Correlation ID linking to audit records
    /// </summary>
    public string? CorrelationId { get; set; }
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
    /// The resulting identity IDs after the split
    /// </summary>
    public List<Guid> ResultingIdentityIds { get; set; } = new();

    /// <summary>
    /// The actor (user/system) who performed the split
    /// </summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the split
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// When the split occurred
    /// </summary>
    public DateTime SplitAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Correlation ID linking to audit records
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Tracks the lineage and history of identity changes
/// </summary>
public class IdentityLineage
{
    /// <summary>
    /// The person ID this lineage tracks
    /// </summary>
    public Guid PersonId { get; set; }

    /// <summary>
    /// Chronological list of merge events
    /// </summary>
    public List<MergeEvent> MergeHistory { get; set; } = new();

    /// <summary>
    /// Chronological list of split events
    /// </summary>
    public List<SplitEvent> SplitHistory { get; set; } = new();

    /// <summary>
    /// When this lineage was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this lineage was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Match request with all inputs and computed features for auditability
/// </summary>
public class MatchRequest
{
    /// <summary>
    /// Unique identifier for this match request
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The original input identity (normalized)
    /// </summary>
    public Identity InputIdentity { get; set; } = null!;

    /// <summary>
    /// Tokenized/encrypted sensitive fields
    /// </summary>
    public Dictionary<string, string> EncryptedFields { get; set; } = new();

    /// <summary>
    /// Computed feature vectors used in matching
    /// </summary>
    public Dictionary<string, object> ComputedFeatures { get; set; } = new();

    /// <summary>
    /// The match result with all scores
    /// </summary>
    public MatchResult MatchResult { get; set; } = null!;

    /// <summary>
    /// Final decision made
    /// </summary>
    public ResolutionDecision Decision { get; set; }

    /// <summary>
    /// Algorithm version used
    /// </summary>
    public string? AlgorithmVersion { get; set; }

    /// <summary>
    /// Configuration used for matching
    /// </summary>
    public MatchingConfiguration Configuration { get; set; } = null!;

    /// <summary>
    /// When this request was processed
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Processing time
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Actor who initiated the request
    /// </summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// Source system that made the request
    /// </summary>
    public string? SourceSystem { get; set; }

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    public string? CorrelationId { get; set; }
}