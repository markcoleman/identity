using IdentityResolution.Core.Models;

namespace IdentityResolution.Core.Services;

/// <summary>
/// Service for managing identity resolution review queue
/// </summary>
public interface IReviewQueueService
{
    /// <summary>
    /// Add an identity to the review queue
    /// </summary>
    /// <param name="identity">The identity requiring review</param>
    /// <param name="matchResult">The match result that triggered the review</param>
    /// <param name="reason">Reason for requiring review</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Review queue entry</returns>
    Task<ReviewQueueEntry> AddToReviewQueueAsync(Identity identity, MatchResult matchResult, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending review queue entries
    /// </summary>
    /// <param name="skip">Number to skip for pagination</param>
    /// <param name="take">Number to take for pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pending review entries</returns>
    Task<IEnumerable<ReviewQueueEntry>> GetPendingReviewsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an entry in the review queue
/// </summary>
public class ReviewQueueEntry
{
    /// <summary>
    /// Unique identifier for this review entry
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The identity requiring review
    /// </summary>
    public Identity Identity { get; set; } = null!;

    /// <summary>
    /// The match result that triggered the review
    /// </summary>
    public MatchResult MatchResult { get; set; } = null!;

    /// <summary>
    /// Reason for requiring review
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Status of this review entry
    /// </summary>
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    /// <summary>
    /// When this entry was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this entry was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User assigned to review this entry
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// Priority of this review
    /// </summary>
    public ReviewPriority Priority { get; set; } = ReviewPriority.Normal;
}

/// <summary>
/// Status of a review queue entry
/// </summary>
public enum ReviewStatus
{
    Pending,
    InReview,
    Completed,
    Rejected
}

/// <summary>
/// Priority levels for review queue entries
/// </summary>
public enum ReviewPriority
{
    Low,
    Normal,
    High,
    Urgent
}
