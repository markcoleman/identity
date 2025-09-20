using System.Collections.Concurrent;
using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.Extensions.Logging;

namespace IdentityResolution.Core.Services.Implementations;

/// <summary>
/// In-memory implementation of review queue service for development/testing
/// </summary>
public class InMemoryReviewQueueService : IReviewQueueService
{
    private readonly ConcurrentDictionary<Guid, ReviewQueueItem> _reviewItems = new();
    private readonly IIdentityStorageService _storageService;
    private readonly ILogger<InMemoryReviewQueueService> _logger;

    public InMemoryReviewQueueService(
        IIdentityStorageService storageService,
        ILogger<InMemoryReviewQueueService> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    public Task<ReviewQueueItem> AddToReviewQueueAsync(ReviewQueueItem item, CancellationToken cancellationToken = default)
    {
        if (item.Id == Guid.Empty)
            item.Id = Guid.NewGuid();

        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        item.Status = ReviewStatus.Open;

        _reviewItems.TryAdd(item.Id, item);

        _logger.LogInformation("Added review queue item {ItemId} for identity {IdentityId}",
            item.Id, item.SourceIdentity.Id);

        return Task.FromResult(item);
    }

    public Task<IEnumerable<ReviewQueueItem>> GetOpenReviewItemsAsync(CancellationToken cancellationToken = default)
    {
        var openItems = _reviewItems.Values
            .Where(item => item.Status == ReviewStatus.Open || item.Status == ReviewStatus.InProgress)
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.CreatedAt);

        return Task.FromResult(openItems.AsEnumerable());
    }

    public Task<ReviewQueueItem?> GetReviewItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _reviewItems.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task<ReviewQueueItem> UpdateReviewItemAsync(Guid id, ResolutionDecision decision, string reviewedBy, string? notes = null, CancellationToken cancellationToken = default)
    {
        if (!_reviewItems.TryGetValue(id, out var item))
            throw new ArgumentException($"Review item {id} not found", nameof(id));

        item.ReviewerDecision = decision;
        item.ReviewedBy = reviewedBy;
        item.ReviewerNotes = notes;
        item.ReviewedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        item.Status = ReviewStatus.Reviewed;

        _logger.LogInformation("Updated review queue item {ItemId} with decision {Decision} by {ReviewedBy}",
            id, decision, reviewedBy);

        return Task.FromResult(item);
    }

    public async Task<ResolutionResult> ResolveReviewItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_reviewItems.TryGetValue(id, out var item))
            throw new ArgumentException($"Review item {id} not found", nameof(id));

        if (item.Status != ReviewStatus.Reviewed || !item.ReviewerDecision.HasValue)
            throw new InvalidOperationException($"Review item {id} has not been reviewed");

        // Execute the reviewer's decision
        ResolutionResult result;

        switch (item.ReviewerDecision.Value)
        {
            case ResolutionDecision.Auto:
                // Simple merge approach - in a real system this would be more sophisticated
                if (item.CandidateIdentities.Any())
                {
                    var primaryIdentity = item.CandidateIdentities.First();
                    var mergedIdentity = MergeSimple(primaryIdentity, item.SourceIdentity);
                    var storedIdentity = await _storageService.UpdateIdentityAsync(mergedIdentity, cancellationToken);

                    result = new ResolutionResult
                    {
                        EPID = storedIdentity.Attributes.GetValueOrDefault("EPID", $"EPID-{storedIdentity.Id:N}"),
                        ResolvedIdentity = storedIdentity,
                        Decision = ResolutionDecision.Auto,
                        WasAutoMerged = true,
                        Explanation = "Manual review approved automatic merge"
                    };
                }
                else
                {
                    throw new InvalidOperationException("Cannot merge - no candidate identities found");
                }
                break;

            case ResolutionDecision.New:
                // Create new identity
                var newIdentity = await _storageService.StoreIdentityAsync(item.SourceIdentity, cancellationToken);
                newIdentity.Attributes["EPID"] = $"EPID-{newIdentity.Id:N}";
                newIdentity = await _storageService.UpdateIdentityAsync(newIdentity, cancellationToken);

                result = new ResolutionResult
                {
                    EPID = newIdentity.Attributes["EPID"],
                    ResolvedIdentity = newIdentity,
                    Decision = ResolutionDecision.New,
                    WasAutoMerged = false,
                    Explanation = "Manual review approved new identity creation"
                };
                break;

            default:
                throw new InvalidOperationException($"Invalid reviewer decision: {item.ReviewerDecision.Value}");
        }

        // Mark as resolved
        item.Status = ReviewStatus.Resolved;
        item.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation("Resolved review queue item {ItemId} with decision {Decision}",
            id, item.ReviewerDecision.Value);

        return result;
    }

    /// <summary>
    /// Simple merge logic for review queue resolution
    /// </summary>
    private Identity MergeSimple(Identity primary, Identity secondary)
    {
        // Simple merge - take primary identity and fill in missing fields from secondary
        var merged = new Identity
        {
            Id = primary.Id,
            CreatedAt = primary.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            Source = primary.Source,
            Confidence = Math.Max(primary.Confidence, secondary.Confidence),
            PersonalInfo = new PersonalInfo
            {
                FirstName = primary.PersonalInfo.FirstName ?? secondary.PersonalInfo.FirstName,
                LastName = primary.PersonalInfo.LastName ?? secondary.PersonalInfo.LastName,
                MiddleName = primary.PersonalInfo.MiddleName ?? secondary.PersonalInfo.MiddleName,
                DateOfBirth = primary.PersonalInfo.DateOfBirth ?? secondary.PersonalInfo.DateOfBirth,
                Gender = primary.PersonalInfo.Gender ?? secondary.PersonalInfo.Gender,
                Address = primary.PersonalInfo.Address ?? secondary.PersonalInfo.Address
            },
            ContactInfo = new ContactInfo
            {
                Email = primary.ContactInfo.Email ?? secondary.ContactInfo.Email,
                Phone = primary.ContactInfo.Phone ?? secondary.ContactInfo.Phone,
                AlternatePhone = primary.ContactInfo.AlternatePhone ?? secondary.ContactInfo.AlternatePhone,
                Website = primary.ContactInfo.Website ?? secondary.ContactInfo.Website
            },
            Identifiers = primary.Identifiers.Concat(secondary.Identifiers).ToList(),
            Attributes = new Dictionary<string, string>(primary.Attributes.Concat(secondary.Attributes))
        };

        return merged;
    }
}
