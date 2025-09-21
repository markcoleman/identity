using System.Collections.Concurrent;
using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.Extensions.Logging;

namespace IdentityResolution.Core.Services.Implementations;

/// <summary>
/// In-memory implementation of audit service for development/testing
/// </summary>
public class InMemoryAuditService : IAuditService
{
    private readonly ConcurrentBag<AuditRecord> _auditRecords = new();
    private readonly ConcurrentDictionary<Guid, List<MergeEvent>> _mergeEvents = new();
    private readonly ConcurrentDictionary<Guid, List<SplitEvent>> _splitEvents = new();
    private readonly ConcurrentDictionary<Guid, IdentityLineage> _lineages = new();
    private readonly ConcurrentBag<MatchRequest> _matchRequests = new();
    private readonly ILogger<InMemoryAuditService> _logger;

    public InMemoryAuditService(ILogger<InMemoryAuditService> logger)
    {
        _logger = logger;
    }

    public Task<AuditRecord> RecordAuditAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        // Ensure immutability by creating a new record with init-only properties
        var immutableRecord = record with { Timestamp = DateTime.UtcNow };

        _auditRecords.Add(immutableRecord);

        _logger.LogDebug("Recorded audit event {OperationType} for identity {IdentityId} by {Actor}",
            record.OperationType, record.SourceIdentityId, record.Actor);

        return Task.FromResult(immutableRecord);
    }

    public Task<IEnumerable<AuditRecord>> GetAuditRecordsAsync(Guid identityId, CancellationToken cancellationToken = default)
    {
        var records = _auditRecords
            .Where(r => r.SourceIdentityId == identityId)
            .OrderBy(r => r.Timestamp)
            .AsEnumerable();

        return Task.FromResult(records);
    }

    public Task<IEnumerable<AuditRecord>> GetAuditRecordsByTypeAsync(AuditOperationType operationType, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = _auditRecords.Where(r => r.OperationType == operationType);

        if (from.HasValue)
            query = query.Where(r => r.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.Timestamp <= to.Value);

        var records = query.OrderBy(r => r.Timestamp).AsEnumerable();

        return Task.FromResult(records);
    }

    public Task<AuditRecord?> GetAuditRecordAsync(Guid auditId, CancellationToken cancellationToken = default)
    {
        var record = _auditRecords.FirstOrDefault(r => r.Id == auditId);
        return Task.FromResult(record);
    }

    public Task<MergeEvent> RecordMergeEventAsync(MergeEvent mergeEvent, CancellationToken cancellationToken = default)
    {
        // Record in merge events collection
        _mergeEvents.AddOrUpdate(mergeEvent.PrimaryIdentityId,
            new List<MergeEvent> { mergeEvent },
            (key, existing) => { existing.Add(mergeEvent); return existing; });

        // Update lineage
        UpdateLineageForMerge(mergeEvent);

        _logger.LogDebug("Recorded merge event: {PrimaryId} + {SecondaryId} = {ResultId}",
            mergeEvent.PrimaryIdentityId, mergeEvent.SecondaryIdentityId, mergeEvent.ResultingIdentityId);

        return Task.FromResult(mergeEvent);
    }

    public Task<SplitEvent> RecordSplitEventAsync(SplitEvent splitEvent, CancellationToken cancellationToken = default)
    {
        // Record in split events collection
        _splitEvents.AddOrUpdate(splitEvent.OriginalIdentityId,
            new List<SplitEvent> { splitEvent },
            (key, existing) => { existing.Add(splitEvent); return existing; });

        // Update lineage
        UpdateLineageForSplit(splitEvent);

        _logger.LogDebug("Recorded split event: {OriginalId} -> [{ResultIds}]",
            splitEvent.OriginalIdentityId, string.Join(", ", splitEvent.ResultingIdentityIds));

        return Task.FromResult(splitEvent);
    }

    public Task<IdentityLineage?> GetIdentityLineageAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        _lineages.TryGetValue(personId, out var lineage);
        return Task.FromResult(lineage);
    }

    public Task<MatchRequest> RecordMatchRequestAsync(MatchRequest matchRequest, CancellationToken cancellationToken = default)
    {
        _matchRequests.Add(matchRequest);

        _logger.LogDebug("Recorded match request {MatchId} for identity {IdentityId}",
            matchRequest.Id, matchRequest.InputIdentity.Id);

        return Task.FromResult(matchRequest);
    }

    public Task<MatchRequest?> GetMatchRequestAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var matchRequest = _matchRequests.FirstOrDefault(m => m.Id == matchId);
        return Task.FromResult(matchRequest);
    }

    public Task<IEnumerable<MatchRequest>> GetMatchRequestsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var requests = _matchRequests
            .Where(r => r.ProcessedAt >= from && r.ProcessedAt <= to)
            .OrderBy(r => r.ProcessedAt)
            .AsEnumerable();

        return Task.FromResult(requests);
    }

    private void UpdateLineageForMerge(MergeEvent mergeEvent)
    {
        // Get or create lineage for the resulting identity
        var lineage = _lineages.AddOrUpdate(mergeEvent.ResultingIdentityId,
            new IdentityLineage
            {
                PersonId = mergeEvent.ResultingIdentityId,
                MergeHistory = new List<MergeEvent> { mergeEvent }
            },
            (key, existing) =>
            {
                existing.MergeHistory.Add(mergeEvent);
                existing.UpdatedAt = DateTime.UtcNow;
                return existing;
            });
    }

    private void UpdateLineageForSplit(SplitEvent splitEvent)
    {
        // Update lineage for each resulting identity
        foreach (var resultingId in splitEvent.ResultingIdentityIds)
        {
            _lineages.AddOrUpdate(resultingId,
                new IdentityLineage
                {
                    PersonId = resultingId,
                    SplitHistory = new List<SplitEvent> { splitEvent }
                },
                (key, existing) =>
                {
                    existing.SplitHistory.Add(splitEvent);
                    existing.UpdatedAt = DateTime.UtcNow;
                    return existing;
                });
        }
    }
}