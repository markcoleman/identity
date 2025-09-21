using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityResolution.Api.Controllers;

/// <summary>
/// API controller for reprocessing and replay operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReprocessingController : ControllerBase
{
    private readonly IReprocessingService _reprocessingService;
    private readonly ILogger<ReprocessingController> _logger;

    public ReprocessingController(
        IReprocessingService reprocessingService,
        ILogger<ReprocessingController> logger)
    {
        _reprocessingService = reprocessingService;
        _logger = logger;
    }

    /// <summary>
    /// Replay a specific match request with a newer algorithm
    /// </summary>
    /// <param name="request">Replay request parameters</param>
    [HttpPost("replay")]
    public async Task<ActionResult<ReprocessingResult>> ReplayMatch([FromBody] ReplayRequest request)
    {
        try
        {
            var result = await _reprocessingService.ReplayMatchAsync(
                request.OriginalMatchId,
                request.NewAlgorithmVersion,
                request.Configuration);

            _logger.LogInformation("Replayed match with algorithm version. Decision changed: {DecisionChanged}",
                result.DecisionChanged);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid replay request parameters");
            return BadRequest("Invalid replay request parameters");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replaying match");
            return StatusCode(500, "Error occurred during match replay");
        }
    }

    /// <summary>
    /// Start batch reprocessing of matches within a date range
    /// </summary>
    /// <param name="request">Batch reprocessing request</param>
    [HttpPost("batch")]
    public async Task<ActionResult<Guid>> StartBatchReprocessing([FromBody] BatchReprocessingJobRequest request)
    {
        try
        {
            if (request.FromDate >= request.ToDate)
            {
                return BadRequest("FromDate must be before ToDate");
            }

            if (string.IsNullOrEmpty(request.NewAlgorithmVersion))
            {
                return BadRequest("NewAlgorithmVersion is required");
            }

            var jobId = await _reprocessingService.ScheduleBatchReprocessingAsync(request);

            _logger.LogInformation("Started batch reprocessing job");

            return CreatedAtAction(nameof(GetBatchJobStatus), new { jobId }, jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting batch reprocessing");
            return StatusCode(500, "Error occurred while starting batch reprocessing");
        }
    }

    /// <summary>
    /// Get status of a batch reprocessing job
    /// </summary>
    /// <param name="jobId">Batch job ID</param>
    [HttpGet("batch/{jobId}/status")]
    public async Task<ActionResult<BatchJobStatus>> GetBatchJobStatus(Guid jobId)
    {
        try
        {
            var status = await _reprocessingService.GetBatchJobStatusAsync(jobId);
            
            if (status.Status == JobStatus.Failed && status.ErrorMessage == "Job not found")
            {
                return NotFound();
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving batch job status");
            return StatusCode(500, "Error occurred while retrieving job status");
        }
    }

    /// <summary>
    /// Compare results between original and reprocessed matches
    /// </summary>
    /// <param name="originalMatchId">Original match ID</param>
    /// <param name="reprocessedMatchId">Reprocessed match ID</param>
    [HttpGet("compare/{originalMatchId}/{reprocessedMatchId}")]
    public async Task<ActionResult<MatchComparisonResult>> CompareMatches(Guid originalMatchId, Guid reprocessedMatchId)
    {
        try
        {
            var comparison = await _reprocessingService.CompareMatchResultsAsync(originalMatchId, reprocessedMatchId);
            return Ok(comparison);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid comparison request for matches {Original} and {Reprocessed}",
                originalMatchId, reprocessedMatchId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing matches");
            return StatusCode(500, "Error occurred during match comparison");
        }
    }

    /// <summary>
    /// Get reprocessing history for a specific match
    /// </summary>
    /// <param name="originalMatchId">Original match ID</param>
    [HttpGet("history/{originalMatchId}")]
    public async Task<ActionResult<IEnumerable<ReprocessingResult>>> GetReprocessingHistory(Guid originalMatchId)
    {
        try
        {
            var history = await _reprocessingService.GetReprocessingHistoryAsync(originalMatchId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reprocessing history");
            return StatusCode(500, "Error occurred while retrieving reprocessing history");
        }
    }

    /// <summary>
    /// Synchronous batch reprocessing for smaller datasets (use with caution)
    /// </summary>
    /// <param name="request">Batch reprocessing request</param>
    [HttpPost("batch/sync")]
    public async Task<ActionResult<BatchReprocessingResult>> BatchReprocessingSync([FromBody] BatchReprocessingSyncRequest request)
    {
        try
        {
            if (request.FromDate >= request.ToDate)
            {
                return BadRequest("FromDate must be before ToDate");
            }

            // Limit synchronous processing to prevent timeouts
            var maxDays = 30;
            if ((request.ToDate - request.FromDate).TotalDays > maxDays)
            {
                return BadRequest($"Synchronous processing is limited to {maxDays} days. Use async batch processing for larger ranges.");
            }

            var result = await _reprocessingService.BatchReplayMatchesAsync(
                request.FromDate,
                request.ToDate,
                request.NewAlgorithmVersion,
                request.Configuration);

            _logger.LogInformation("Completed synchronous batch reprocessing. Processed: {Count}",
                result.SuccessfullyProcessed);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during synchronous batch reprocessing");
            return StatusCode(500, "Error occurred during batch reprocessing");
        }
    }
}

/// <summary>
/// Request model for replaying a single match
/// </summary>
public class ReplayRequest
{
    /// <summary>
    /// ID of the original match to replay
    /// </summary>
    public Guid OriginalMatchId { get; set; }

    /// <summary>
    /// New algorithm version to use
    /// </summary>
    public string NewAlgorithmVersion { get; set; } = string.Empty;

    /// <summary>
    /// New matching configuration (optional)
    /// </summary>
    public MatchingConfiguration? Configuration { get; set; }
}

/// <summary>
/// Request model for synchronous batch reprocessing
/// </summary>
public class BatchReprocessingSyncRequest
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
}