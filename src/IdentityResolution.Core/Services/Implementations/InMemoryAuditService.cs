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
            .OrderByDescending(r => r.Timestamp);

        return Task.FromResult(records.AsEnumerable());
    }

    public Task<IEnumerable<AuditRecord>> GetAuditRecordsByTypeAsync(AuditOperationType operationType, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = _auditRecords.Where(r => r.OperationType == operationType);

        if (from is not null)
            query = query.Where(r => r.Timestamp >= from.Value);

        if (to is not null)
            query = query.Where(r => r.Timestamp <= to.Value);

        var records = query.OrderByDescending(r => r.Timestamp);

        return Task.FromResult(records.AsEnumerable());
    }

    public Task<MergeEvent> RecordMergeEventAsync(MergeEvent mergeEvent, CancellationToken cancellationToken = default)
    {
        // Record the merge event for both identities involved
        _mergeEvents.AddOrUpdate(mergeEvent.PrimaryIdentityId,
            new List<MergeEvent> { mergeEvent },
            (key, list) => { list.Add(mergeEvent); return list; });

        _mergeEvents.AddOrUpdate(mergeEvent.SecondaryIdentityId,
            new List<MergeEvent> { mergeEvent },
            (key, list) => { list.Add(mergeEvent); return list; });

        // Update lineage
        UpdateLineageForMerge(mergeEvent);

        // Create audit record
        var auditRecord = new AuditRecord
        {
            OperationType = AuditOperationType.Merge,
            SourceIdentityId = mergeEvent.PrimaryIdentityId,
            Actor = mergeEvent.MergedBy,
            Score = mergeEvent.ConfidenceScore,
            Decision = ResolutionDecision.Auto,
            Inputs = new Dictionary<string, object>
            {
                ["primaryIdentityId"] = mergeEvent.PrimaryIdentityId,
                ["secondaryIdentityId"] = mergeEvent.SecondaryIdentityId,
                ["isAutomatic"] = mergeEvent.IsAutomatic
            },
            Result = new Dictionary<string, object>
            {
                ["resultingIdentityId"] = mergeEvent.ResultingIdentityId,
                ["reason"] = mergeEvent.Reason ?? ""
            },
            Metadata = mergeEvent.Context
        };

        _auditRecords.Add(auditRecord);

        _logger.LogInformation("Recorded merge event: {PrimaryId} + {SecondaryId} → {ResultId}",
            mergeEvent.PrimaryIdentityId, mergeEvent.SecondaryIdentityId, mergeEvent.ResultingIdentityId);

        return Task.FromResult(mergeEvent);
    }

    public Task<SplitEvent> RecordSplitEventAsync(SplitEvent splitEvent, CancellationToken cancellationToken = default)
    {
        _splitEvents.AddOrUpdate(splitEvent.OriginalIdentityId,
            new List<SplitEvent> { splitEvent },
            (key, list) => { list.Add(splitEvent); return list; });

        // Record for each resulting identity too
        foreach (var resultId in splitEvent.ResultingIdentityIds)
        {
            _splitEvents.AddOrUpdate(resultId,
                new List<SplitEvent> { splitEvent },
                (key, list) => { list.Add(splitEvent); return list; });
        }

        // Update lineage
        UpdateLineageForSplit(splitEvent);

        // Create audit record
        var auditRecord = new AuditRecord
        {
            OperationType = AuditOperationType.Split,
            SourceIdentityId = splitEvent.OriginalIdentityId,
            Actor = splitEvent.SplitBy,
            Inputs = new Dictionary<string, object>
            {
                ["originalIdentityId"] = splitEvent.OriginalIdentityId,
                ["reason"] = splitEvent.Reason
            },
            Result = new Dictionary<string, object>
            {
                ["resultingIdentityIds"] = splitEvent.ResultingIdentityIds,
                ["splitCount"] = splitEvent.ResultingIdentityIds.Count
            },
            Metadata = splitEvent.Context
        };

        _auditRecords.Add(auditRecord);

        _logger.LogInformation("Recorded split event: {OriginalId} → [{ResultIds}]",
            splitEvent.OriginalIdentityId, string.Join(", ", splitEvent.ResultingIdentityIds));

        return Task.FromResult(splitEvent);
    }

    public Task<IdentityLineage> GetIdentityLineageAsync(Guid identityId, CancellationToken cancellationToken = default)
    {
        if (_lineages.TryGetValue(identityId, out var lineage))
        {
            return Task.FromResult(lineage);
        }

        // Create new lineage if none exists
        var newLineage = new IdentityLineage
        {
            IdentityId = identityId,
            MergeEvents = _mergeEvents.TryGetValue(identityId, out var merges) ? merges : new List<MergeEvent>(),
            SplitEvents = _splitEvents.TryGetValue(identityId, out var splits) ? splits : new List<SplitEvent>()
        };

        _lineages.TryAdd(identityId, newLineage);
        return Task.FromResult(newLineage);
    }

    private void UpdateLineageForMerge(MergeEvent mergeEvent)
    {
        // Update lineage for primary identity
        _lineages.AddOrUpdate(mergeEvent.PrimaryIdentityId,
            new IdentityLineage
            {
                IdentityId = mergeEvent.PrimaryIdentityId,
                MergeEvents = new List<MergeEvent> { mergeEvent }
            },
            (key, lineage) =>
            {
                lineage.MergeEvents.Add(mergeEvent);
                lineage.UpdatedAt = DateTime.UtcNow;
                return lineage;
            });

        // Update lineage for secondary identity
        _lineages.AddOrUpdate(mergeEvent.SecondaryIdentityId,
            new IdentityLineage
            {
                IdentityId = mergeEvent.SecondaryIdentityId,
                MergeEvents = new List<MergeEvent> { mergeEvent }
            },
            (key, lineage) =>
            {
                lineage.MergeEvents.Add(mergeEvent);
                lineage.UpdatedAt = DateTime.UtcNow;
                return lineage;
            });

        // Create lineage for resulting identity
        _lineages.AddOrUpdate(mergeEvent.ResultingIdentityId,
            new IdentityLineage
            {
                IdentityId = mergeEvent.ResultingIdentityId,
                MergeEvents = new List<MergeEvent> { mergeEvent }
            },
            (key, lineage) =>
            {
                lineage.MergeEvents.Add(mergeEvent);
                lineage.UpdatedAt = DateTime.UtcNow;
                return lineage;
            });
    }

    private void UpdateLineageForSplit(SplitEvent splitEvent)
    {
        // Update lineage for original identity
        _lineages.AddOrUpdate(splitEvent.OriginalIdentityId,
            new IdentityLineage
            {
                IdentityId = splitEvent.OriginalIdentityId,
                SplitEvents = new List<SplitEvent> { splitEvent }
            },
            (key, lineage) =>
            {
                lineage.SplitEvents.Add(splitEvent);
                lineage.UpdatedAt = DateTime.UtcNow;
                return lineage;
            });

        // Update lineage for each resulting identity
        foreach (var resultId in splitEvent.ResultingIdentityIds)
        {
            _lineages.AddOrUpdate(resultId,
                new IdentityLineage
                {
                    IdentityId = resultId,
                    SplitEvents = new List<SplitEvent> { splitEvent }
                },
                (key, lineage) =>
                {
                    lineage.SplitEvents.Add(splitEvent);
                    lineage.UpdatedAt = DateTime.UtcNow;
                    return lineage;
                });
        }
    }
}
