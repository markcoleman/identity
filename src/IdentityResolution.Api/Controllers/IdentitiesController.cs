using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdentityResolution.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IdentitiesController : ControllerBase
{
    private readonly IIdentityStorageService _storageService;
    private readonly IDataNormalizationService _normalizationService;
    private readonly ILogger<IdentitiesController> _logger;

    public IdentitiesController(
        IIdentityStorageService storageService,
        IDataNormalizationService normalizationService,
        ILogger<IdentitiesController> logger)
    {
        _storageService = storageService;
        _normalizationService = normalizationService;
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
    public async Task<IActionResult> DeleteIdentity(Guid id)
    {
        var deleted = await _storageService.DeleteIdentityAsync(id);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
