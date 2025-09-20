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

    public AuditController(
        IAuditService auditService,
        ILogger<AuditController> logger)
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
    public async Task<ActionResult<IEnumerable<AuditRecord>>> GetAuditRecords(Guid identityId)
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
    /// Get audit records by operation type
    /// </summary>
    /// <param name="operationType">The operation type to filter by</param>
    /// <param name="from">Start date (optional)</param>
    /// <param name="to">End date (optional)</param>
    [HttpGet("operations/{operationType}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<AuditRecord>>> GetAuditRecordsByType(
        AuditOperationType operationType,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var records = await _auditService.GetAuditRecordsByTypeAsync(operationType, from, to);
            return Ok(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit records for operation type {OperationType}", operationType);
            return StatusCode(500, "Error occurred while retrieving audit records");
        }
    }

    /// <summary>
    /// Get identity lineage (merge/split history)
    /// </summary>
    /// <param name="identityId">The identity ID</param>
    [HttpGet("lineage/{identityId}")]
    [Authorize]
    public async Task<ActionResult<IdentityLineage>> GetIdentityLineage(Guid identityId)
    {
        try
        {
            var lineage = await _auditService.GetIdentityLineageAsync(identityId);
            return Ok(lineage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lineage for identity {IdentityId}", identityId);
            return StatusCode(500, "Error occurred while retrieving identity lineage");
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
}
