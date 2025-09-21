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
    private readonly ConcurrentBag<ReviewQueueEntry> _reviewQueue = new();
    private readonly ILogger<InMemoryReviewQueueService> _logger;

    public InMemoryReviewQueueService(ILogger<InMemoryReviewQueueService> logger)
    {
        _logger = logger;
    }

    public Task<ReviewQueueEntry> AddToReviewQueueAsync(Identity identity, MatchResult matchResult, string reason, CancellationToken cancellationToken = default)
    {
        var entry = new ReviewQueueEntry
        {
            Identity = identity,
            MatchResult = matchResult,
            Reason = reason,
            Status = ReviewStatus.Pending,
            Priority = DetermineReviewPriority(matchResult)
        };

        _reviewQueue.Add(entry);

        _logger.LogInformation("Added identity {IdentityId} to review queue: {Reason}", identity.Id, reason);

        return Task.FromResult(entry);
    }

    public Task<IEnumerable<ReviewQueueEntry>> GetPendingReviewsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var pendingReviews = _reviewQueue
            .Where(r => r.Status == ReviewStatus.Pending)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .Skip(skip)
            .Take(take);

        return Task.FromResult(pendingReviews);
    }

    private ReviewPriority DetermineReviewPriority(MatchResult matchResult)
    {
        // Simple priority logic based on match scores
        var bestScore = matchResult.Matches.FirstOrDefault()?.OverallScore ?? 0.0;

        if (bestScore > 0.85)
            return ReviewPriority.High; // High confidence matches need quick review

        if (matchResult.Matches.Count > 5)
            return ReviewPriority.High; // Many matches indicate complex case

        return ReviewPriority.Normal;
    }
}