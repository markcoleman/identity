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
    /// </summary>
    /// <param name="identity">The identity to resolve</param>
    [HttpPost("resolve")]
    [Authorize] // Add authorization requirement for resolve operations
    public async Task<ActionResult<ResolutionResult>> ResolveIdentity([FromBody] Identity identity)
    {
        try
        {
            var resolutionResult = await _resolutionService.ResolveIdentityAsync(identity);

            _logger.LogInformation("Identity resolution completed with decision {Decision} for resolution {ResolutionId}", 
                resolutionResult.Decision, resolutionResult.ResolutionId);

            return Ok(resolutionResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving identity");
            return StatusCode(500, "Error occurred during identity resolution");
        }
    }
}
