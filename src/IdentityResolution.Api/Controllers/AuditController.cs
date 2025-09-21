using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityResolution.Api.Controllers;

/// <summary>
/// API controller for audit trail and compliance operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IAuditService auditService, ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Get audit records for a specific identity
    /// </summary>
    /// <param name="identityId">The identity ID</param>
    [HttpGet("identity/{identityId}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<AuditRecord>>> GetAuditRecordsForIdentity(Guid identityId)
    {
        try
        {
            var records = await _auditService.GetAuditRecordsAsync(identityId);
            return Ok(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit records for identity {IdentityId}", identityId);
            return StatusCode(500, "Error occurred while retrieving audit records");
        }
    }

    /// <summary>
    /// Get a specific audit record by ID
    /// </summary>
    /// <param name="auditId">The audit record ID</param>
    [HttpGet("{auditId}")]
    [Authorize]
    public async Task<ActionResult<AuditRecord>> GetAuditRecord(Guid auditId)
    {
        try
        {
            var record = await _auditService.GetAuditRecordAsync(auditId);
            if (record == null)
            {
                return NotFound();
            }
            return Ok(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit record {AuditId}", auditId);
            return StatusCode(500, "Error occurred while retrieving audit record");
        }
    }

    /// <summary>
    /// Get all merge events within a date range
    /// </summary>
    /// <param name="from">Start date (optional)</param>
    /// <param name="to">End date (optional)</param>
    [HttpGet("merges")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<AuditRecord>>> GetMergeEvents(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var records = await _auditService.GetAuditRecordsByTypeAsync(AuditOperationType.Merge, from, to);
            return Ok(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving merge events");
            return StatusCode(500, "Error occurred while retrieving merge events");
        }
    }

    /// <summary>
    /// Get all resolution audit records within a date range
    /// </summary>
    /// <param name="from">Start date (optional)</param>
    /// <param name="to">End date (optional)</param>
    [HttpGet("resolutions")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<AuditRecord>>> GetResolutionEvents(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var records = await _auditService.GetAuditRecordsByTypeAsync(AuditOperationType.Resolve, from, to);
            return Ok(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resolution events");
            return StatusCode(500, "Error occurred while retrieving resolution events");
        }
    }

    /// <summary>
    /// Get all matching audit records within a date range
    /// </summary>
    /// <param name="from">Start date (optional)</param>
    /// <param name="to">End date (optional)</param>
    [HttpGet("matches")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<AuditRecord>>> GetMatchEvents(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var records = await _auditService.GetAuditRecordsByTypeAsync(AuditOperationType.Match, from, to);
            return Ok(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving match events");
            return StatusCode(500, "Error occurred while retrieving match events");
        }
    }

    /// <summary>
    /// Get identity lineage (merge and split history)
    /// </summary>
    /// <param name="personId">The person ID</param>
    [HttpGet("lineage/{personId}")]
    [Authorize]
    public async Task<ActionResult<IdentityLineage>> GetIdentityLineage(Guid personId)
    {
        try
        {
            var lineage = await _auditService.GetIdentityLineageAsync(personId);
            if (lineage == null)
            {
                return NotFound();
            }
            return Ok(lineage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving identity lineage for {PersonId}", personId);
            return StatusCode(500, "Error occurred while retrieving identity lineage");
        }
    }

    /// <summary>
    /// Get a specific match request by ID with all computed features
    /// </summary>
    /// <param name="matchId">The match request ID</param>
    [HttpGet("match/{matchId}")]
    [Authorize]
    public async Task<ActionResult<MatchRequest>> GetMatchRequest(Guid matchId)
    {
        try
        {
            var matchRequest = await _auditService.GetMatchRequestAsync(matchId);
            if (matchRequest == null)
            {
                return NotFound();
            }
            return Ok(matchRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving match request {MatchId}", matchId);
            return StatusCode(500, "Error occurred while retrieving match request");
        }
    }
}
