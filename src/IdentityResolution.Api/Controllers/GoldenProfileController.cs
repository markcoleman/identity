using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityResolution.Api.Controllers;

/// <summary>
/// API controller for golden profile management
/// </summary>
[ApiController]
[Route("api/identity")]
public class GoldenProfileController : ControllerBase
{
    private readonly IGoldenProfileService _goldenProfileService;
    private readonly IAuditService _auditService;
    private readonly ILogger<GoldenProfileController> _logger;

    public GoldenProfileController(
        IGoldenProfileService goldenProfileService, 
        IAuditService auditService,
        ILogger<GoldenProfileController> logger)
    {
        _goldenProfileService = goldenProfileService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Get golden profile by person ID (must return within 200ms P99)
    /// </summary>
    /// <param name="personId">The person ID</param>
    [HttpGet("person/{personId}")]
    public async Task<ActionResult<GoldenProfile>> GetGoldenProfile(Guid personId)
    {
        try
        {
            var profile = await _goldenProfileService.GetGoldenProfileAsync(personId);
            if (profile == null)
            {
                return NotFound();
            }
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving golden profile for person {PersonId}", personId);
            return StatusCode(500, "Error occurred while retrieving golden profile");
        }
    }

    /// <summary>
    /// Get golden profile by EPID
    /// </summary>
    /// <param name="epid">The enterprise person ID</param>
    [HttpGet("epid/{epid}")]
    public async Task<ActionResult<GoldenProfile>> GetGoldenProfileByEpid(string epid)
    {
        try
        {
            var profile = await _goldenProfileService.GetGoldenProfileByEpidAsync(epid);
            if (profile == null)
            {
                return NotFound();
            }
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving golden profile for EPID");
            return StatusCode(500, "Error occurred while retrieving golden profile");
        }
    }

    /// <summary>
    /// Get historical versions of a golden profile (must return within 2s for 90% of cases)
    /// </summary>
    /// <param name="personId">The person ID</param>
    [HttpGet("history/{personId}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<GoldenProfile>>> GetGoldenProfileHistory(Guid personId)
    {
        try
        {
            var history = await _goldenProfileService.GetGoldenProfileHistoryAsync(personId);
            return Ok(history);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid golden profile creation request");
            return BadRequest("Invalid golden profile data");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving golden profile history");
            return StatusCode(500, "Error occurred while retrieving golden profile history");
        }
    }

    /// <summary>
    /// Get all golden profiles with pagination
    /// </summary>
    /// <param name="skip">Number of records to skip</param>
    /// <param name="take">Number of records to take (max 100)</param>
    [HttpGet("profiles")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<GoldenProfile>>> GetGoldenProfiles(
        [FromQuery] int skip = 0, 
        [FromQuery] int take = 50)
    {
        try
        {
            // Enforce maximum page size for performance
            take = Math.Min(take, 100);

            var profiles = await _goldenProfileService.GetGoldenProfilesAsync(skip, take);
            return Ok(profiles);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for golden profiles");
            return BadRequest("Invalid request parameters");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving golden profiles");
            return StatusCode(500, "Error occurred while retrieving golden profiles");
        }
    }

    /// <summary>
    /// Search golden profiles by attributes
    /// </summary>
    /// <param name="searchRequest">Search criteria</param>
    [HttpPost("search")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<GoldenProfile>>> SearchGoldenProfiles([FromBody] SearchRequest searchRequest)
    {
        try
        {
            var profiles = await _goldenProfileService.SearchGoldenProfilesAsync(searchRequest.Criteria);
            return Ok(profiles);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid search request for golden profiles");
            return BadRequest("Invalid search criteria");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching golden profiles");
            return StatusCode(500, "Error occurred while searching golden profiles");
        }
    }

    /// <summary>
    /// Create a new golden profile
    /// </summary>
    /// <param name="profile">The golden profile to create</param>
    [HttpPost("profile")]
    [Authorize]
    public async Task<ActionResult<GoldenProfile>> CreateGoldenProfile([FromBody] GoldenProfile profile)
    {
        try
        {
            var createdProfile = await _goldenProfileService.CreateGoldenProfileAsync(profile);

            // Record audit trail
            var auditRecord = new AuditRecord
            {
                OperationType = AuditOperationType.Create,
                SourceIdentityId = createdProfile.PersonId,
                Actor = "System", // In production, get from current user context
                SourceSystem = "API",
                Results = new Dictionary<string, object>
                {
                    ["PersonId"] = createdProfile.PersonId,
                    ["EPID"] = createdProfile.EPID ?? "",
                    ["Status"] = createdProfile.Status.ToString()
                }
            };

            await _auditService.RecordAuditAsync(auditRecord);

            _logger.LogInformation("Created golden profile with ID {PersonId}", 
                createdProfile.PersonId);

            return CreatedAtAction(
                nameof(GetGoldenProfile), 
                new { personId = createdProfile.PersonId }, 
                createdProfile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating golden profile");
            return StatusCode(500, "Error occurred while creating golden profile");
        }
    }

    /// <summary>
    /// Update a golden profile (transaction-safe with optimistic concurrency)
    /// </summary>
    /// <param name="personId">The person ID</param>
    /// <param name="profile">The updated golden profile</param>
    [HttpPut("person/{personId}")]
    [Authorize]
    public async Task<ActionResult<GoldenProfile>> UpdateGoldenProfile(Guid personId, [FromBody] GoldenProfile profile)
    {
        if (personId != profile.PersonId)
        {
            return BadRequest("PersonId mismatch");
        }

        try
        {
            var updatedProfile = await _goldenProfileService.UpdateGoldenProfileAsync(profile);

            // Record audit trail
            var auditRecord = new AuditRecord
            {
                OperationType = AuditOperationType.Update,
                SourceIdentityId = personId,
                Actor = "System", // In production, get from current user context
                SourceSystem = "API",
                Results = new Dictionary<string, object>
                {
                    ["PersonId"] = updatedProfile.PersonId,
                    ["Version"] = updatedProfile.Version,
                    ["UpdatedAt"] = updatedProfile.UpdatedAt
                }
            };

            await _auditService.RecordAuditAsync(auditRecord);

            return Ok(updatedProfile);
        }
        catch (OptimisticConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Optimistic concurrency conflict updating golden profile {PersonId}", personId);
            return Conflict("Profile has been modified by another operation");
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating golden profile {PersonId}", personId);
            return StatusCode(500, "Error occurred while updating golden profile");
        }
    }

    /// <summary>
    /// Merge golden profiles (transaction-safe)
    /// </summary>
    /// <param name="mergeRequest">The merge request</param>
    [HttpPost("merge")]
    [Authorize]
    public async Task<ActionResult<GoldenProfile>> MergeGoldenProfiles([FromBody] MergeRequest mergeRequest)
    {
        try
        {
            var mergedProfile = await _goldenProfileService.MergeGoldenProfilesAsync(
                mergeRequest.PrimaryPersonId, 
                mergeRequest.SecondaryPersonId, 
                mergeRequest.Actor);

            // Record merge event
            var mergeEvent = new MergeEvent
            {
                PrimaryIdentityId = mergeRequest.PrimaryPersonId,
                SecondaryIdentityId = mergeRequest.SecondaryPersonId,
                ResultingIdentityId = mergedProfile.PersonId,
                Actor = mergeRequest.Actor,
                Reason = mergeRequest.Reason,
                ConfidenceScore = mergeRequest.ConfidenceScore
            };

            await _auditService.RecordMergeEventAsync(mergeEvent);

            _logger.LogInformation("Merged golden profiles successfully");

            return Ok(mergedProfile);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid merge request parameters");
            return BadRequest("Invalid merge request parameters");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging golden profiles");
            return StatusCode(500, "Error occurred while merging golden profiles");
        }
    }

    /// <summary>
    /// Split a golden profile into multiple profiles
    /// </summary>
    /// <param name="splitRequest">The split request</param>
    [HttpPost("split")]
    [Authorize]
    public async Task<ActionResult<List<GoldenProfile>>> SplitGoldenProfile([FromBody] SplitRequest splitRequest)
    {
        try
        {
            var resultProfiles = await _goldenProfileService.SplitGoldenProfileAsync(
                splitRequest.PersonId, 
                splitRequest.SplitIdentityIds, 
                splitRequest.Actor);

            // Record split event
            var splitEvent = new SplitEvent
            {
                OriginalIdentityId = splitRequest.PersonId,
                ResultingIdentityIds = resultProfiles.Select(p => p.PersonId).ToList(),
                Actor = splitRequest.Actor,
                Reason = splitRequest.Reason
            };

            await _auditService.RecordSplitEventAsync(splitEvent);

            _logger.LogInformation("Split golden profile into {Count} profiles",
                resultProfiles.Count);

            return Ok(resultProfiles);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid split request parameters");
            return BadRequest("Invalid split request parameters");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error splitting golden profile");
            return StatusCode(500, "Error occurred while splitting golden profile");
        }
    }
}

/// <summary>
/// Request model for searching golden profiles
/// </summary>
public class SearchRequest
{
    public Dictionary<string, object> Criteria { get; set; } = new();
}

/// <summary>
/// Request model for merging golden profiles
/// </summary>
public class MergeRequest
{
    public Guid PrimaryPersonId { get; set; }
    public Guid SecondaryPersonId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// Request model for splitting golden profiles
/// </summary>
public class SplitRequest
{
    public Guid PersonId { get; set; }
    public List<Guid> SplitIdentityIds { get; set; } = new();
    public string Actor { get; set; } = string.Empty;
    public string? Reason { get; set; }
}