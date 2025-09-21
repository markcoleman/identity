using IdentityResolution.Core.Models;

namespace IdentityResolution.Core.Services;

/// <summary>
/// Service for batch processing of identity records for resolution
/// </summary>
public interface IBatchProcessingService
{
    /// <summary>
    /// Process a batch of identities from a stream (CSV/JSON)
    /// </summary>
    /// <param name="stream">Input stream containing identity data</param>
    /// <param name="format">Format of the input data (csv, json)</param>
    /// <param name="configuration">Processing configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch processing result</returns>
    Task<BatchProcessingResult> ProcessBatchAsync(
        Stream stream,
        BatchInputFormat format,
        BatchProcessingConfiguration? configuration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedule a batch processing job for background execution
    /// </summary>
    /// <param name="jobRequest">Batch processing job request</param>
    /// <returns>Job ID for tracking</returns>
    Task<Guid> ScheduleBatchProcessingAsync(BatchProcessingJobRequest jobRequest);

    /// <summary>
    /// Get status of a batch processing job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>Job status and progress</returns>
    Task<BatchJobStatus> GetBatchJobStatusAsync(Guid jobId);

    /// <summary>
    /// Get results of a completed batch processing job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="format">Output format (csv, json)</param>
    /// <returns>Results stream</returns>
    Task<Stream> GetBatchResultsAsync(Guid jobId, BatchOutputFormat format = BatchOutputFormat.Json);

    /// <summary>
    /// Cancel a running batch processing job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>True if job was cancelled successfully</returns>
    Task<bool> CancelBatchJobAsync(Guid jobId);
}

/// <summary>
/// Configuration for batch processing operations
/// </summary>
public class BatchProcessingConfiguration
{
    /// <summary>
    /// Maximum number of records to process in parallel
    /// </summary>
    public int MaxParallelism { get; set; } = 10;

    /// <summary>
    /// Batch size for chunking records
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum file size in bytes (default 50MB)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Matching configuration to use
    /// </summary>
    public MatchingConfiguration? MatchingConfiguration { get; set; }

    /// <summary>
    /// Whether to continue processing if individual records fail
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Maximum number of errors before stopping the entire batch
    /// </summary>
    public int MaxErrorsBeforeStop { get; set; } = 1000;

    /// <summary>
    /// Timeout for the entire batch operation
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(2);
}

/// <summary>
/// Request for batch processing job
/// </summary>
public class BatchProcessingJobRequest
{
    /// <summary>
    /// Source of the batch data (file path, blob storage URI, etc.)
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Input format of the data
    /// </summary>
    public BatchInputFormat InputFormat { get; set; } = BatchInputFormat.Json;

    /// <summary>
    /// Processing configuration
    /// </summary>
    public BatchProcessingConfiguration Configuration { get; set; } = new();

    /// <summary>
    /// Actor who requested the batch processing
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>
    /// Optional description for the batch job
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether to publish completion events
    /// </summary>
    public bool PublishEvents { get; set; } = false;
}

/// <summary>
/// Result of batch processing operation
/// </summary>
public class BatchProcessingResult
{
    /// <summary>
    /// Unique ID for this batch operation
    /// </summary>
    public Guid BatchId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Total number of records processed
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Number of records successfully processed
    /// </summary>
    public int SuccessfullyProcessed { get; set; }

    /// <summary>
    /// Number of records that failed processing
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Breakdown by resolution decision
    /// </summary>
    public Dictionary<ResolutionDecision, int> DecisionCounts { get; set; } = new();

    /// <summary>
    /// Individual processing results
    /// </summary>
    public List<BatchRecordResult> Results { get; set; } = new();

    /// <summary>
    /// Processing errors encountered
    /// </summary>
    public List<BatchProcessingError> Errors { get; set; } = new();

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

    /// <summary>
    /// Source information
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Processing configuration used
    /// </summary>
    public BatchProcessingConfiguration? Configuration { get; set; }
}

/// <summary>
/// Result for a single record in a batch processing operation
/// </summary>
public class BatchRecordResult
{
    /// <summary>
    /// Record number in the batch (1-based)
    /// </summary>
    public int RecordNumber { get; set; }

    /// <summary>
    /// Original identity data (for reference)
    /// </summary>
    public Identity? InputIdentity { get; set; }

    /// <summary>
    /// Resolved identity (if successful)
    /// </summary>
    public Identity? ResolvedIdentity { get; set; }

    /// <summary>
    /// Enterprise Person ID assigned
    /// </summary>
    public string? EPID { get; set; }

    /// <summary>
    /// Resolution decision made
    /// </summary>
    public ResolutionDecision Decision { get; set; }

    /// <summary>
    /// Confidence score for the resolution
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Matches found during processing
    /// </summary>
    public List<IdentityMatch> Matches { get; set; } = new();

    /// <summary>
    /// Explanation of the resolution decision
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Processing time for this record
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Whether processing was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code if processing failed
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Features used in the matching process
    /// </summary>
    public Dictionary<string, object> Features { get; set; } = new();
}

/// <summary>
/// Error information for batch processing
/// </summary>
public class BatchProcessingError
{
    /// <summary>
    /// Record number where the error occurred (if applicable)
    /// </summary>
    public int? RecordNumber { get; set; }

    /// <summary>
    /// Error code
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Error message
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Exception details (if applicable)
    /// </summary>
    public string? ExceptionDetails { get; set; }

    /// <summary>
    /// When the error occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Input format enumeration
/// </summary>
public enum BatchInputFormat
{
    /// <summary>
    /// JSON format
    /// </summary>
    Json,

    /// <summary>
    /// CSV format
    /// </summary>
    Csv
}

/// <summary>
/// Output format enumeration
/// </summary>
public enum BatchOutputFormat
{
    /// <summary>
    /// JSON format
    /// </summary>
    Json,

    /// <summary>
    /// CSV format
    /// </summary>
    Csv
}

/// <summary>
/// Common error codes for batch processing
/// </summary>
public static class BatchErrorCodes
{
    public const string InvalidFormat = "INVALID_FORMAT";
    public const string MalformedDateOfBirth = "MALFORMED_DOB";
    public const string MissingRequiredField = "MISSING_REQUIRED_FIELD";
    public const string InvalidEmail = "INVALID_EMAIL";
    public const string InvalidPhone = "INVALID_PHONE";
    public const string ProcessingTimeout = "PROCESSING_TIMEOUT";
    public const string MatchingServiceError = "MATCHING_SERVICE_ERROR";
    public const string StorageServiceError = "STORAGE_SERVICE_ERROR";
    public const string ValidationError = "VALIDATION_ERROR";
}
