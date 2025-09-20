using System;
using System.Collections.Generic;

namespace IdentityResolution.Core.Models;

/// <summary>
/// Represents a review queue item for manual adjudication
/// </summary>
public class ReviewQueueItem
{
    /// <summary>
    /// Unique identifier for the review item
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The original identity that requires review
    /// </summary>
    public Identity SourceIdentity { get; set; } = null!;

    /// <summary>
    /// Candidate identities that might be matches
    /// </summary>
    public List<Identity> CandidateIdentities { get; set; } = new();

    /// <summary>
    /// Match scores and feature details
    /// </summary>
    public List<IdentityMatch> Matches { get; set; } = new();

    /// <summary>
    /// System's initial decision that triggered the review
    /// </summary>
    public ResolutionDecision SystemDecision { get; set; }

    /// <summary>
    /// Current status of the review item
    /// </summary>
    public ReviewStatus Status { get; set; } = ReviewStatus.Open;

    /// <summary>
    /// When this item was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this item was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who is assigned to review this item
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// User who reviewed this item
    /// </summary>
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// When the review was completed
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Final decision made by the reviewer
    /// </summary>
    public ResolutionDecision? ReviewerDecision { get; set; }

    /// <summary>
    /// Notes from the reviewer
    /// </summary>
    public string? ReviewerNotes { get; set; }

    /// <summary>
    /// Priority level for this review (1 = high, 5 = low)
    /// </summary>
    public int Priority { get; set; } = 3;

    /// <summary>
    /// Source system that submitted this identity
    /// </summary>
    public string? SourceSystem { get; set; }

    /// <summary>
    /// Additional context for the reviewer
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Status of a review queue item
/// </summary>
public enum ReviewStatus
{
    /// <summary>
    /// Waiting for review
    /// </summary>
    Open,

    /// <summary>
    /// Currently being reviewed
    /// </summary>
    InProgress,

    /// <summary>
    /// Review completed with a decision
    /// </summary>
    Reviewed,

    /// <summary>
    /// Final resolution applied and identity created/merged
    /// </summary>
    Resolved,

    /// <summary>
    /// Review was escalated to a higher level
    /// </summary>
    Escalated,

    /// <summary>
    /// Review was cancelled or expired
    /// </summary>
    Cancelled
}
