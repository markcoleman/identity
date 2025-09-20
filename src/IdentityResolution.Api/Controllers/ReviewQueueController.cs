using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityResolution.Api.Controllers;

/// <summary>
/// API controller for managing review queue operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ReviewQueueController : ControllerBase
{
    private readonly IReviewQueueService _reviewQueueService;
    private readonly ILogger<ReviewQueueController> _logger;

    public ReviewQueueController(
        IReviewQueueService reviewQueueService,
        ILogger<ReviewQueueController> logger)
    {
        _reviewQueueService = reviewQueueService;
        _logger = logger;
    }

    /// <summary>
    /// Get all open review queue items
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<ReviewQueueItem>>> GetOpenReviewItems()
    {
        try
        {
            var items = await _reviewQueueService.GetOpenReviewItemsAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving open review items");
            return StatusCode(500, "Error occurred while retrieving review items");
        }
    }

    /// <summary>
    /// Get a specific review queue item
    /// </summary>
    /// <param name="id">The review item ID</param>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<ReviewQueueItem>> GetReviewItem(Guid id)
    {
        try
        {
            var item = await _reviewQueueService.GetReviewItemAsync(id);

            if (item == null)
            {
                return NotFound($"Review item {id} not found");
            }

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review item {ReviewItemId}", id);
            return StatusCode(500, "Error occurred while retrieving review item");
        }
    }

    /// <summary>
    /// Update a review queue item with a decision
    /// </summary>
    /// <param name="id">The review item ID</param>
    /// <param name="request">The review decision request</param>
    [HttpPut("{id}/decision")]
    [Authorize]
    public async Task<ActionResult<ReviewQueueItem>> UpdateReviewDecision(Guid id, [FromBody] ReviewDecisionRequest request)
    {
        try
        {
            var updatedItem = await _reviewQueueService.UpdateReviewItemAsync(
                id,
                request.Decision,
                request.ReviewedBy,
                request.Notes);

            _logger.LogInformation("Updated review item {ReviewItemId} with decision {Decision} by {ReviewedBy}",
                id, request.Decision, SanitizeLogInput(request.ReviewedBy));

            return Ok(updatedItem);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Review item {ReviewItemId} not found", id);
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for review item {ReviewItemId}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating review item {ReviewItemId}", id);
            return StatusCode(500, "An unexpected error occurred while updating review item");
        }
    }

    /// <summary>
    /// Resolve a review queue item and execute the decision
    /// </summary>
    /// <param name="id">The review item ID</param>
    [HttpPost("{id}/resolve")]
    [Authorize]
    public async Task<ActionResult<ResolutionResult>> ResolveReviewItem(Guid id)
    {
        try
        {
            var result = await _reviewQueueService.ResolveReviewItemAsync(id);

            _logger.LogInformation("Resolved review item {ReviewItemId} with EPID {EPID}",
                id, result.EPID);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Review item {ReviewItemId} not found", id);
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot resolve review item {ReviewItemId}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error resolving review item {ReviewItemId}", id);
            return StatusCode(500, "An unexpected error occurred while resolving review item");
        }
    }

    /// <summary>
    /// Sanitize user input for logging to prevent log injection attacks
    /// </summary>
    /// <param name="input">The input to sanitize</param>
    /// <returns>Sanitized input safe for logging</returns>
    private static string SanitizeLogInput(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return "[empty]";

        // Remove or replace characters that could be used for log injection
        return input.Replace('\r', ' ')
                   .Replace('\n', ' ')
                   .Replace('\t', ' ')
                   .Trim();
    }
}

/// <summary>
/// Request model for updating review decisions
/// </summary>
public class ReviewDecisionRequest
{
    /// <summary>
    /// The decision made by the reviewer
    /// </summary>
    public ResolutionDecision Decision { get; set; }

    /// <summary>
    /// The reviewer's username or ID
    /// </summary>
    public string ReviewedBy { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes from the reviewer
    /// </summary>
    public string? Notes { get; set; }
}
