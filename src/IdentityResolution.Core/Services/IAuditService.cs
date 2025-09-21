using IdentityResolution.Core.Models;

namespace IdentityResolution.Core.Services;

/// <summary>
/// Service for recording and retrieving audit information
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Record an audit event
    /// </summary>
    /// <param name="record">The audit record to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The stored audit record</returns>
    Task<AuditRecord> RecordAuditAsync(AuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit records for a specific identity
    /// </summary>
    /// <param name="identityId">The identity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of audit records</returns>
    Task<IEnumerable<AuditRecord>> GetAuditRecordsAsync(Guid identityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit records by operation type within a date range
    /// </summary>
    /// <param name="operationType">Type of operation</param>
    /// <param name="from">Start date (optional)</param>
    /// <param name="to">End date (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of audit records</returns>
    Task<IEnumerable<AuditRecord>> GetAuditRecordsByTypeAsync(AuditOperationType operationType, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific audit record by ID
    /// </summary>
    /// <param name="auditId">The audit record ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The audit record or null if not found</returns>
    Task<AuditRecord?> GetAuditRecordAsync(Guid auditId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a merge event
    /// </summary>
    /// <param name="mergeEvent">The merge event to record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The recorded merge event</returns>
    Task<MergeEvent> RecordMergeEventAsync(MergeEvent mergeEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a split event
    /// </summary>
    /// <param name="splitEvent">The split event to record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The recorded split event</returns>
    Task<SplitEvent> RecordSplitEventAsync(SplitEvent splitEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get identity lineage (merge and split history)
    /// </summary>
    /// <param name="personId">The person ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Identity lineage or null if not found</returns>
    Task<IdentityLineage?> GetIdentityLineageAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store a match request with all computed features
    /// </summary>
    /// <param name="matchRequest">The match request to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The stored match request</returns>
    Task<MatchRequest> RecordMatchRequestAsync(MatchRequest matchRequest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific match request by ID
    /// </summary>
    /// <param name="matchId">The match request ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The match request or null if not found</returns>
    Task<MatchRequest?> GetMatchRequestAsync(Guid matchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get match requests within a date range for reprocessing
    /// </summary>
    /// <param name="from">Start date</param>
    /// <param name="to">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of match requests</returns>
    Task<IEnumerable<MatchRequest>> GetMatchRequestsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
