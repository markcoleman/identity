using IdentityResolution.Core.Models;

namespace IdentityResolution.Core.Services;

/// <summary>
/// Service for reprocessing/replaying match attempts with newer algorithms
/// </summary>
public interface IReprocessingService
{
    /// <summary>
    /// Replay a specific match request with a newer algorithm version
    /// </summary>
    /// <param name="originalMatchId">ID of the original match request</param>
    /// <param name="newAlgorithmVersion">Version of the new algorithm to use</param>
    /// <param name="configuration">New matching configuration (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New match result from reprocessing</returns>
    Task<ReprocessingResult> ReplayMatchAsync(Guid originalMatchId, string newAlgorithmVersion, MatchingConfiguration? configuration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch replay of match requests within a date range
    /// </summary>
    /// <param name="fromDate">Start date of matches to replay</param>
    /// <param name="toDate">End date of matches to replay</param>
    /// <param name="newAlgorithmVersion">Version of the new algorithm to use</param>
    /// <param name="configuration">New matching configuration (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch processing results</returns>
    Task<BatchReprocessingResult> BatchReplayMatchesAsync(DateTime fromDate, DateTime toDate, string newAlgorithmVersion, MatchingConfiguration? configuration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compare results between original and reprocessed matches
    /// </summary>
    /// <param name="originalMatchId">ID of the original match</param>
    /// <param name="reprocessedMatchId">ID of the reprocessed match</param>
    /// <returns>Comparison analysis</returns>
    Task<MatchComparisonResult> CompareMatchResultsAsync(Guid originalMatchId, Guid reprocessedMatchId);

    /// <summary>
    /// Get all reprocessing attempts for a specific match
    /// </summary>
    /// <param name="originalMatchId">ID of the original match</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of reprocessing attempts</returns>
    Task<IEnumerable<ReprocessingResult>> GetReprocessingHistoryAsync(Guid originalMatchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedule batch reprocessing job for background execution
    /// </summary>
    /// <param name="jobRequest">Reprocessing job configuration</param>
    /// <returns>Job ID for tracking</returns>
    Task<Guid> ScheduleBatchReprocessingAsync(BatchReprocessingJobRequest jobRequest);

    /// <summary>
    /// Get status of a batch reprocessing job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>Job status and progress</returns>
    Task<BatchJobStatus> GetBatchJobStatusAsync(Guid jobId);
}

/// <summary>
/// Result of reprocessing a single match request
/// </summary>
public class ReprocessingResult
{
    /// <summary>
    /// Unique ID for this reprocessing attempt
    /// </summary>
    public Guid ReprocessingId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID of the original match request
    /// </summary>
    public Guid OriginalMatchId { get; set; }

    /// <summary>
    /// The original match request data
    /// </summary>
    public MatchRequest? OriginalMatchRequest { get; set; }

    /// <summary>
    /// New match result from reprocessing
    /// </summary>
    public MatchRequest? ReprocessedMatchRequest { get; set; }

    /// <summary>
    /// Algorithm version used for reprocessing
    /// </summary>
    public string NewAlgorithmVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether the decision changed between original and reprocessed
    /// </summary>
    public bool DecisionChanged { get; set; }

    /// <summary>
    /// Whether the confidence scores changed significantly
    /// </summary>
    public bool SignificantScoreChange { get; set; }

    /// <summary>
    /// When the reprocessing was performed
    /// </summary>
    public DateTime ReprocessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Processing time for the reprocessing
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Any errors or warnings during reprocessing
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Actor who initiated the reprocessing
    /// </summary>
    public string Actor { get; set; } = string.Empty;
}

/// <summary>
/// Result of batch reprocessing operation
/// </summary>
public class BatchReprocessingResult
{
    /// <summary>
    /// Unique ID for this batch operation
    /// </summary>
    public Guid BatchId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Total number of matches eligible for reprocessing
    /// </summary>
    public int TotalMatches { get; set; }

    /// <summary>
    /// Number of matches successfully reprocessed
    /// </summary>
    public int SuccessfullyProcessed { get; set; }

    /// <summary>
    /// Number of matches that failed reprocessing
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Number of matches where decision changed
    /// </summary>
    public int DecisionChanges { get; set; }

    /// <summary>
    /// Number of matches with significant score changes
    /// </summary>
    public int SignificantScoreChanges { get; set; }

    /// <summary>
    /// Individual reprocessing results
    /// </summary>
    public List<ReprocessingResult> Results { get; set; } = new();

    /// <summary>
    /// Total processing time for the batch
    /// </summary>
    public TimeSpan TotalProcessingTime { get; set; }

    /// <summary>
    /// When the batch processing started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the batch processing completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Comparison result between two match attempts
/// </summary>
public class MatchComparisonResult
{
    /// <summary>
    /// Original match details
    /// </summary>
    public MatchRequest OriginalMatch { get; set; } = null!;

    /// <summary>
    /// Reprocessed match details
    /// </summary>
    public MatchRequest ReprocessedMatch { get; set; } = null!;

    /// <summary>
    /// Whether the final decision changed
    /// </summary>
    public bool DecisionChanged { get; set; }

    /// <summary>
    /// Difference in confidence scores
    /// </summary>
    public Dictionary<string, double> ScoreDifferences { get; set; } = new();

    /// <summary>
    /// New matches that weren't found in original
    /// </summary>
    public List<IdentityMatch> NewMatches { get; set; } = new();

    /// <summary>
    /// Matches that were lost in reprocessing
    /// </summary>
    public List<IdentityMatch> LostMatches { get; set; } = new();

    /// <summary>
    /// Overall similarity score between results (0.0 to 1.0)
    /// </summary>
    public double SimilarityScore { get; set; }

    /// <summary>
    /// Analysis summary
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Request for batch reprocessing job
/// </summary>
public class BatchReprocessingJobRequest
{
    /// <summary>
    /// Start date for matches to reprocess
    /// </summary>
    public DateTime FromDate { get; set; }

    /// <summary>
    /// End date for matches to reprocess  
    /// </summary>
    public DateTime ToDate { get; set; }

    /// <summary>
    /// New algorithm version to use
    /// </summary>
    public string NewAlgorithmVersion { get; set; } = string.Empty;

    /// <summary>
    /// New matching configuration (optional)
    /// </summary>
    public MatchingConfiguration? Configuration { get; set; }

    /// <summary>
    /// Maximum number of matches to process in parallel
    /// </summary>
    public int MaxParallelism { get; set; } = 5;

    /// <summary>
    /// Whether to update golden profiles with new results
    /// </summary>
    public bool UpdateGoldenProfiles { get; set; } = false;

    /// <summary>
    /// Actor requesting the batch job
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Status of a batch reprocessing job
/// </summary>
public class BatchJobStatus
{
    /// <summary>
    /// Unique job ID
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Current status of the job
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// Number of items processed so far
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Total number of items to process
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage => TotalCount > 0 ? (double)ProcessedCount / TotalCount * 100 : 0;

    /// <summary>
    /// When the job was started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Estimated completion time
    /// </summary>
    public DateTime? EstimatedCompletionAt { get; set; }

    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Current operation being performed
    /// </summary>
    public string? CurrentOperation { get; set; }
}

/// <summary>
/// Job status enumeration
/// </summary>
public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}