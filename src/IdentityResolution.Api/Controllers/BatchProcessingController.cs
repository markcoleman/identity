using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityResolution.Api.Controllers;

/// <summary>
/// API controller for batch processing of identity records
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BatchProcessingController : ControllerBase
{
    private readonly IBatchProcessingService _batchProcessingService;
    private readonly ILogger<BatchProcessingController> _logger;

    public BatchProcessingController(
        IBatchProcessingService batchProcessingService,
        ILogger<BatchProcessingController> logger)
    {
        _batchProcessingService = batchProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// Upload and process a batch of identity records
    /// </summary>
    /// <param name="file">CSV or JSON file containing identity records</param>
    /// <param name="format">Format of the input file (csv, json)</param>
    /// <param name="maxParallelism">Maximum number of records to process in parallel</param>
    /// <param name="batchSize">Batch size for chunking records</param>
    /// <param name="continueOnError">Whether to continue processing if individual records fail</param>
    [HttpPost("upload")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB limit
    public async Task<ActionResult<BatchProcessingResult>> UploadAndProcessBatch(
        IFormFile file,
        [FromForm] string format = "json",
        [FromForm] int maxParallelism = 10,
        [FromForm] int batchSize = 100,
        [FromForm] bool continueOnError = true)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided or file is empty");
            }

            // Validate file format
            if (!Enum.TryParse<BatchInputFormat>(format, true, out var inputFormat))
            {
                return BadRequest($"Invalid format. Supported formats: {string.Join(", ", Enum.GetNames<BatchInputFormat>())}");
            }

            // Validate file extension matches format
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var expectedExtension = inputFormat == BatchInputFormat.Json ? ".json" : ".csv";

            if (extension != expectedExtension)
            {
                return BadRequest($"File extension '{extension}' does not match specified format '{format}'");
            }

            // Configure batch processing
            var configuration = new BatchProcessingConfiguration
            {
                MaxParallelism = Math.Max(1, Math.Min(maxParallelism, 50)), // Limit to reasonable range
                BatchSize = Math.Max(1, Math.Min(batchSize, 1000)), // Limit to reasonable range
                ContinueOnError = continueOnError,
                MaxFileSizeBytes = 100 * 1024 * 1024 // 100MB
            };

            // Validate file size
            if (file.Length > configuration.MaxFileSizeBytes)
            {
                return BadRequest($"File size ({file.Length} bytes) exceeds maximum allowed size ({configuration.MaxFileSizeBytes} bytes)");
            }

            _logger.LogInformation("Starting batch processing of file {FileName} ({FileSize} bytes) with format {Format}",
                file.FileName, file.Length, inputFormat);

            // Process the batch
            using var stream = file.OpenReadStream();
            var result = await _batchProcessingService.ProcessBatchAsync(stream, inputFormat, configuration);

            _logger.LogInformation("Batch processing completed for file {FileName}. Processed {Successful}/{Total} records",
                file.FileName, result.SuccessfullyProcessed, result.TotalRecords);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch file {FileName}", file?.FileName);
            return StatusCode(500, "Error occurred during batch processing");
        }
    }

    /// <summary>
    /// Schedule a batch processing job for background execution
    /// </summary>
    /// <param name="request">Batch processing job request</param>
    [HttpPost("schedule")]
    public async Task<ActionResult<Guid>> ScheduleBatchProcessing([FromBody] BatchProcessingJobRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Source))
            {
                return BadRequest("Source is required");
            }

            if (string.IsNullOrWhiteSpace(request.RequestedBy))
            {
                return BadRequest("RequestedBy is required");
            }

            // Validate configuration
            if (request.Configuration.MaxParallelism <= 0 || request.Configuration.MaxParallelism > 100)
            {
                return BadRequest("MaxParallelism must be between 1 and 100");
            }

            if (request.Configuration.BatchSize <= 0 || request.Configuration.BatchSize > 10000)
            {
                return BadRequest("BatchSize must be between 1 and 10000");
            }

            var jobId = await _batchProcessingService.ScheduleBatchProcessingAsync(request);

            _logger.LogInformation("Scheduled batch processing job {JobId} for source {Source} by {RequestedBy}",
                jobId, request.Source, request.RequestedBy);

            return CreatedAtAction(nameof(GetBatchJobStatus), new { jobId }, jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling batch processing job");
            return StatusCode(500, "Error occurred while scheduling batch processing job");
        }
    }

    /// <summary>
    /// Get status of a batch processing job
    /// </summary>
    /// <param name="jobId">Batch job ID</param>
    [HttpGet("{jobId}/status")]
    public async Task<ActionResult<BatchJobStatus>> GetBatchJobStatus(Guid jobId)
    {
        try
        {
            var status = await _batchProcessingService.GetBatchJobStatusAsync(jobId);

            if (status.Status == JobStatus.Failed && status.ErrorMessage == "Job not found")
            {
                return NotFound();
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving batch job status for {JobId}", jobId);
            return StatusCode(500, "Error occurred while retrieving job status");
        }
    }

    /// <summary>
    /// Download results of a completed batch processing job
    /// </summary>
    /// <param name="jobId">Batch job ID</param>
    /// <param name="format">Output format (json, csv)</param>
    [HttpGet("{jobId}/results")]
    public async Task<IActionResult> DownloadBatchResults(Guid jobId, [FromQuery] string format = "json")
    {
        try
        {
            if (!Enum.TryParse<BatchOutputFormat>(format, true, out var outputFormat))
            {
                return BadRequest($"Invalid format. Supported formats: {string.Join(", ", Enum.GetNames<BatchOutputFormat>())}");
            }

            var resultsStream = await _batchProcessingService.GetBatchResultsAsync(jobId, outputFormat);

            var contentType = outputFormat == BatchOutputFormat.Json ? "application/json" : "text/csv";
            var fileName = $"batch-results-{jobId}.{format.ToLowerInvariant()}";

            return File(resultsStream, contentType, fileName);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading batch results for {JobId}", jobId);
            return StatusCode(500, "Error occurred while downloading batch results");
        }
    }

    /// <summary>
    /// Cancel a running batch processing job
    /// </summary>
    /// <param name="jobId">Batch job ID</param>
    [HttpPost("{jobId}/cancel")]
    public async Task<IActionResult> CancelBatchJob(Guid jobId)
    {
        try
        {
            var cancelled = await _batchProcessingService.CancelBatchJobAsync(jobId);

            if (!cancelled)
            {
                return NotFound("Job not found or cannot be cancelled");
            }

            _logger.LogInformation("Cancelled batch processing job {JobId}", jobId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling batch job {JobId}", jobId);
            return StatusCode(500, "Error occurred while cancelling job");
        }
    }

    /// <summary>
    /// Get batch processing statistics and metrics
    /// </summary>
    [HttpGet("stats")]
    public Task<ActionResult<BatchProcessingStats>> GetBatchProcessingStats()
    {
        try
        {
            // This would typically come from a metrics service
            // For now, return basic stats
            var stats = new BatchProcessingStats
            {
                TotalJobsProcessed = 0, // Would be tracked in real implementation
                AverageThroughputPerHour = 50000, // Example throughput
                CurrentActiveJobs = 0, // Would query active jobs
                LastUpdated = DateTime.UtcNow
            };

            return Task.FromResult<ActionResult<BatchProcessingStats>>(Ok(stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving batch processing statistics");
            return Task.FromResult<ActionResult<BatchProcessingStats>>(StatusCode(500, "Error occurred while retrieving statistics"));
        }
    }
}

/// <summary>
/// Batch processing statistics
/// </summary>
public class BatchProcessingStats
{
    /// <summary>
    /// Total number of batch jobs processed
    /// </summary>
    public int TotalJobsProcessed { get; set; }

    /// <summary>
    /// Average throughput in records per hour
    /// </summary>
    public int AverageThroughputPerHour { get; set; }

    /// <summary>
    /// Number of currently active jobs
    /// </summary>
    public int CurrentActiveJobs { get; set; }

    /// <summary>
    /// When these statistics were last updated
    /// </summary>
    public DateTime LastUpdated { get; set; }
}
