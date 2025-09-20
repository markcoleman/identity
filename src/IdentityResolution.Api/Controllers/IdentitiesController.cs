using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityResolution.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IdentitiesController : ControllerBase
{
    private readonly IIdentityStorageService _storageService;
    private readonly IDataNormalizationService _normalizationService;
    private readonly IIdentityMatchingService _matchingService;
    private readonly IIdentityResolutionService _resolutionService;
    private readonly ILogger<IdentitiesController> _logger;

    public IdentitiesController(
        IIdentityStorageService storageService,
        IDataNormalizationService normalizationService,
        IIdentityMatchingService matchingService,
        IIdentityResolutionService resolutionService,
        ILogger<IdentitiesController> logger)
    {
        _storageService = storageService;
        _normalizationService = normalizationService;
        _matchingService = matchingService;
        _resolutionService = resolutionService;
        _logger = logger;
    }

    /// <summary>
    /// Get all identities
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Identity>>> GetIdentities()
    {
        var identities = await _storageService.GetAllIdentitiesAsync();
        return Ok(identities);
    }

    /// <summary>
    /// Get an identity by ID
    /// </summary>
    /// <param name="id">The identity ID</param>
    [HttpGet("{id}")]
    public async Task<ActionResult<Identity>> GetIdentity(Guid id)
    {
        var identity = await _storageService.GetIdentityAsync(id);

        if (identity == null)
        {
            return NotFound();
        }

        return Ok(identity);
    }

    /// <summary>
    /// Create a new identity
    /// </summary>
    /// <param name="identity">The identity to create</param>
    [HttpPost]
    [Authorize] // Add authorization requirement for create operations
    public async Task<ActionResult<Identity>> CreateIdentity([FromBody] Identity identity)
    {
        try
        {
            // Normalize the identity data
            var normalizedIdentity = _normalizationService.NormalizeIdentity(identity);

            // Store the identity
            var storedIdentity = await _storageService.StoreIdentityAsync(normalizedIdentity);

            _logger.LogInformation("Created identity {IdentityId}", storedIdentity.Id);

            return CreatedAtAction(
                nameof(GetIdentity),
                new { id = storedIdentity.Id },
                storedIdentity);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid identity data provided");
            return BadRequest("Invalid identity data");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error creating identity");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update an existing identity
    /// </summary>
    /// <param name="id">The identity ID</param>
    /// <param name="identity">The updated identity data</param>
    [HttpPut("{id}")]
    [Authorize] // Add authorization requirement for update operations
    public async Task<ActionResult<Identity>> UpdateIdentity(Guid id, [FromBody] Identity identity)
    {
        if (id != identity.Id)
        {
            return BadRequest("ID mismatch");
        }

        try
        {
            // Check if identity exists
            var existingIdentity = await _storageService.GetIdentityAsync(id);
            if (existingIdentity == null)
            {
                return NotFound();
            }

            // Normalize and update
            var normalizedIdentity = _normalizationService.NormalizeIdentity(identity);
            var updatedIdentity = await _storageService.UpdateIdentityAsync(normalizedIdentity);

            return Ok(updatedIdentity);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error updating identity {IdentityId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete an identity
    /// </summary>
    /// <param name="id">The identity ID</param>
    [HttpDelete("{id}")]
    [Authorize] // Add authorization requirement for delete operations
    public async Task<IActionResult> DeleteIdentity(Guid id)
    {
        var deleted = await _storageService.DeleteIdentityAsync(id);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Find potential matches for an identity
    /// </summary>
    /// <param name="identity">The identity to find matches for</param>
    [HttpPost("match")]
    public async Task<ActionResult<MatchResult>> FindMatches([FromBody] Identity identity)
    {
        try
        {
            var normalizedIdentity = _normalizationService.NormalizeIdentity(identity);
            var matchResult = await _matchingService.FindMatchesAsync(normalizedIdentity);

            _logger.LogInformation("Found {MatchCount} potential matches for identity matching request",
                matchResult.Matches.Count);

            return Ok(matchResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matches for identity");
            return StatusCode(500, "Error occurred while finding matches");
        }
    }

    /// <summary>
    /// Resolve an identity using deterministic and probabilistic matching
    /// Returns EPID, decision, score, and explanations per API contract requirements
    /// </summary>
    /// <param name="identity">The identity to resolve</param>
    [HttpPost("resolve")]
    [Authorize] // Add authorization requirement for resolve operations
    public async Task<ActionResult<IdentityResolutionResponse>> ResolveIdentity([FromBody] Identity identity)
    {
        try
        {
            var resolutionResult = await _resolutionService.ResolveIdentityAsync(identity);

            // Create enhanced API response with EPID, decision, score, and explanations
            var response = new IdentityResolutionResponse
            {
                EPID = resolutionResult.EPID,
                Decision = resolutionResult.Decision,
                Score = resolutionResult.Matches.FirstOrDefault()?.OverallScore ?? 0.0,
                Explanation = resolutionResult.Explanation ?? "No explanation available",
                ResolvedIdentity = resolutionResult.ResolvedIdentity,
                ProcessingTime = resolutionResult.ProcessingTime,
                ResolutionId = resolutionResult.ResolutionId,
                Matches = resolutionResult.Matches.Take(5).ToList(), // Include top 5 matches for context
                WasAutoMerged = resolutionResult.WasAutoMerged,
                Warnings = resolutionResult.Warnings,
                Strategy = resolutionResult.Strategy
            };

            _logger.LogInformation("Identity resolution completed: EPID {EPID}, Decision {Decision}, Score {Score}",
                response.EPID, response.Decision, response.Score);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving identity");
            return StatusCode(500, "Error occurred during identity resolution");
        }
    }
}

/// <summary>
/// Enhanced API response for identity resolution as per requirements
/// </summary>
public class IdentityResolutionResponse
{
    /// <summary>
    /// Enterprise Person ID (EPID) - stable identifier for the resolved person
    /// </summary>
    public string EPID { get; set; } = string.Empty;

    /// <summary>
    /// Resolution decision made by the system
    /// </summary>
    public ResolutionDecision Decision { get; set; }

    /// <summary>
    /// Confidence score for the resolution
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Detailed explanation of how the resolution decision was made
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// The resolved identity
    /// </summary>
    public Identity? ResolvedIdentity { get; set; }

    /// <summary>
    /// Processing time for the resolution
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Unique identifier for this resolution attempt
    /// </summary>
    public Guid ResolutionId { get; set; }

    /// <summary>
    /// Top potential matches found (for transparency)
    /// </summary>
    public List<IdentityMatch> Matches { get; set; } = new();

    /// <summary>
    /// Whether any identities were automatically merged
    /// </summary>
    public bool WasAutoMerged { get; set; }

    /// <summary>
    /// Any warnings or notes about the resolution
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// The resolution strategy used
    /// </summary>
    public string? Strategy { get; set; }
}
