using System.Collections.Concurrent;
using System.Diagnostics;
using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.Extensions.Logging;

namespace IdentityResolution.Core.Services.Implementations;

/// <summary>
/// In-memory implementation of reprocessing service for development/testing
/// </summary>
public class InMemoryReprocessingService : IReprocessingService
{
    private readonly IAuditService _auditService;
    private readonly IIdentityMatchingService _matchingService;
    private readonly IDataNormalizationService _normalizationService;
    private readonly ConcurrentDictionary<Guid, BatchJobStatus> _batchJobs = new();
    private readonly ConcurrentBag<ReprocessingResult> _reprocessingHistory = new();
    private readonly ILogger<InMemoryReprocessingService> _logger;

    public InMemoryReprocessingService(
        IAuditService auditService,
        IIdentityMatchingService matchingService,
        IDataNormalizationService normalizationService,
        ILogger<InMemoryReprocessingService> logger)
    {
        _auditService = auditService;
        _matchingService = matchingService;
        _normalizationService = normalizationService;
        _logger = logger;
    }

    public async Task<ReprocessingResult> ReplayMatchAsync(Guid originalMatchId, string newAlgorithmVersion, MatchingConfiguration? configuration = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        configuration ??= new MatchingConfiguration();

        _logger.LogInformation("Starting replay of match with algorithm version");

        var result = new ReprocessingResult
        {
            OriginalMatchId = originalMatchId,
            NewAlgorithmVersion = newAlgorithmVersion,
            Actor = "System"
        };

        try
        {
            // Get the original match request
            var originalMatch = await _auditService.GetMatchRequestAsync(originalMatchId, cancellationToken);
            if (originalMatch == null)
            {
                result.Warnings.Add("Original match request not found");
                return result;
            }

            result.OriginalMatchRequest = originalMatch;

            // Re-normalize the input identity (in case normalization rules changed)
            var normalizedIdentity = _normalizationService.NormalizeIdentity(originalMatch.InputIdentity);

            // Re-run matching with new algorithm/configuration
            var newMatchResult = await _matchingService.FindMatchesAsync(normalizedIdentity, configuration, cancellationToken);

            // Create new match request for the reprocessed result
            var reprocessedMatch = new MatchRequest
            {
                InputIdentity = normalizedIdentity,
                MatchResult = newMatchResult,
                Decision = DetermineDecision(newMatchResult, configuration),
                Configuration = configuration,
                ProcessingTime = stopwatch.Elapsed,
                Actor = "ReprocessingService",
                SourceSystem = "Reprocessing",
                CorrelationId = result.ReprocessingId.ToString(),
                AlgorithmVersion = newAlgorithmVersion
            };

            result.ReprocessedMatchRequest = reprocessedMatch;

            // Store the reprocessed match request
            await _auditService.RecordMatchRequestAsync(reprocessedMatch, cancellationToken);

            // Analyze differences
            result.DecisionChanged = originalMatch.Decision != reprocessedMatch.Decision;
            result.SignificantScoreChange = HasSignificantScoreChange(originalMatch.MatchResult, newMatchResult);

            // Record audit trail for the reprocessing
            var auditRecord = new AuditRecord
            {
                OperationType = AuditOperationType.Replay,
                SourceIdentityId = originalMatch.InputIdentity.Id,
                Actor = result.Actor,
                SourceSystem = "ReprocessingService",
                CorrelationId = result.ReprocessingId.ToString(),
                AlgorithmVersion = newAlgorithmVersion,
                Inputs = new Dictionary<string, object>
                {
                    ["originalMatchId"] = originalMatchId,
                    ["newAlgorithmVersion"] = newAlgorithmVersion
                },
                Results = new Dictionary<string, object>
                {
                    ["decisionChanged"] = result.DecisionChanged,
                    ["significantScoreChange"] = result.SignificantScoreChange,
                    ["originalDecision"] = originalMatch.Decision.ToString(),
                    ["newDecision"] = reprocessedMatch.Decision.ToString()
                }
            };

            await _auditService.RecordAuditAsync(auditRecord, cancellationToken);

            _logger.LogInformation("Completed replay of match {MatchId}. Decision changed: {DecisionChanged}, Significant score change: {ScoreChanged}",
                originalMatchId, result.DecisionChanged, result.SignificantScoreChange);
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Error during reprocessing: {ex.Message}");
            _logger.LogError(ex, "Error replaying match {MatchId}", originalMatchId);
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            _reprocessingHistory.Add(result);
        }

        return result;
    }

    public async Task<BatchReprocessingResult> BatchReplayMatchesAsync(DateTime fromDate, DateTime toDate, string newAlgorithmVersion, MatchingConfiguration? configuration = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var batchResult = new BatchReprocessingResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Get all match requests in the date range
            var matchRequests = await _auditService.GetMatchRequestsAsync(fromDate, toDate, cancellationToken);
            var matchList = matchRequests.ToList();

            batchResult.TotalMatches = matchList.Count;

            _logger.LogInformation("Starting batch reprocessing of {Count} matches from {FromDate} to {ToDate}",
                matchList.Count, fromDate, toDate);

            // Process each match
            foreach (var originalMatch in matchList)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var reprocessResult = await ReplayMatchAsync(originalMatch.Id, newAlgorithmVersion, configuration, cancellationToken);
                    batchResult.Results.Add(reprocessResult);
                    batchResult.SuccessfullyProcessed++;

                    if (reprocessResult.DecisionChanged)
                        batchResult.DecisionChanges++;

                    if (reprocessResult.SignificantScoreChange)
                        batchResult.SignificantScoreChanges++;
                }
                catch (Exception ex)
                {
                    batchResult.Failed++;
                    _logger.LogWarning(ex, "Failed to reprocess match {MatchId}", originalMatch.Id);
                }
            }

            batchResult.CompletedAt = DateTime.UtcNow;
            stopwatch.Stop();
            batchResult.TotalProcessingTime = stopwatch.Elapsed;

            _logger.LogInformation("Completed batch reprocessing. Processed: {Successful}, Failed: {Failed}, Decision changes: {DecisionChanges}",
                batchResult.SuccessfullyProcessed, batchResult.Failed, batchResult.DecisionChanges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch reprocessing");
            batchResult.CompletedAt = DateTime.UtcNow;
        }

        return batchResult;
    }

    public async Task<MatchComparisonResult> CompareMatchResultsAsync(Guid originalMatchId, Guid reprocessedMatchId)
    {
        var originalMatch = await _auditService.GetMatchRequestAsync(originalMatchId);
        var reprocessedMatch = await _auditService.GetMatchRequestAsync(reprocessedMatchId);

        if (originalMatch == null || reprocessedMatch == null)
        {
            throw new ArgumentException("One or both match requests not found");
        }

        var comparison = new MatchComparisonResult
        {
            OriginalMatch = originalMatch,
            ReprocessedMatch = reprocessedMatch,
            DecisionChanged = originalMatch.Decision != reprocessedMatch.Decision
        };

        // Compare scores
        var originalBestScore = originalMatch.MatchResult.Matches.FirstOrDefault()?.OverallScore ?? 0.0;
        var reprocessedBestScore = reprocessedMatch.MatchResult.Matches.FirstOrDefault()?.OverallScore ?? 0.0;
        
        comparison.ScoreDifferences["BestScore"] = reprocessedBestScore - originalBestScore;

        // Find new matches
        var originalMatchIds = originalMatch.MatchResult.Matches.Select(m => m.CandidateIdentity.Id).ToHashSet();
        comparison.NewMatches = reprocessedMatch.MatchResult.Matches
            .Where(m => !originalMatchIds.Contains(m.CandidateIdentity.Id))
            .ToList();

        // Find lost matches
        var reprocessedMatchIds = reprocessedMatch.MatchResult.Matches.Select(m => m.CandidateIdentity.Id).ToHashSet();
        comparison.LostMatches = originalMatch.MatchResult.Matches
            .Where(m => !reprocessedMatchIds.Contains(m.CandidateIdentity.Id))
            .ToList();

        // Calculate similarity score
        comparison.SimilarityScore = CalculateSimilarityScore(originalMatch.MatchResult, reprocessedMatch.MatchResult);

        // Generate summary
        comparison.Summary = GenerateComparisonSummary(comparison);

        return comparison;
    }

    public Task<IEnumerable<ReprocessingResult>> GetReprocessingHistoryAsync(Guid originalMatchId, CancellationToken cancellationToken = default)
    {
        var history = _reprocessingHistory
            .Where(r => r.OriginalMatchId == originalMatchId)
            .OrderBy(r => r.ReprocessedAt)
            .AsEnumerable();

        return Task.FromResult(history);
    }

    public Task<Guid> ScheduleBatchReprocessingAsync(BatchReprocessingJobRequest jobRequest)
    {
        var jobId = Guid.NewGuid();
        
        var jobStatus = new BatchJobStatus
        {
            JobId = jobId,
            Status = JobStatus.Queued,
            StartedAt = DateTime.UtcNow,
            CurrentOperation = "Initializing batch job"
        };

        _batchJobs[jobId] = jobStatus;

        // In a real implementation, this would queue the job for background processing
        _ = Task.Run(async () =>
        {
            try
            {
                jobStatus.Status = JobStatus.Running;
                jobStatus.CurrentOperation = "Processing matches";

                var result = await BatchReplayMatchesAsync(
                    jobRequest.FromDate, 
                    jobRequest.ToDate, 
                    jobRequest.NewAlgorithmVersion, 
                    jobRequest.Configuration);

                jobStatus.ProcessedCount = result.SuccessfullyProcessed;
                jobStatus.TotalCount = result.TotalMatches;
                jobStatus.Status = JobStatus.Completed;
                jobStatus.CurrentOperation = "Completed";
            }
            catch (Exception ex)
            {
                jobStatus.Status = JobStatus.Failed;
                jobStatus.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Batch reprocessing job {JobId} failed", jobId);
            }
        });

        _logger.LogInformation("Scheduled batch reprocessing job {JobId} for {FromDate} to {ToDate}",
            jobId, jobRequest.FromDate, jobRequest.ToDate);

        return Task.FromResult(jobId);
    }

    public Task<BatchJobStatus> GetBatchJobStatusAsync(Guid jobId)
    {
        _batchJobs.TryGetValue(jobId, out var status);
        return Task.FromResult(status ?? new BatchJobStatus { JobId = jobId, Status = JobStatus.Failed, ErrorMessage = "Job not found" });
    }

    private ResolutionDecision DetermineDecision(MatchResult matchResult, MatchingConfiguration configuration)
    {
        if (!matchResult.Matches.Any())
            return ResolutionDecision.New;

        var bestScore = matchResult.Matches.First().OverallScore;

        if (bestScore >= configuration.AutoMergeThreshold)
            return ResolutionDecision.Auto;

        if (bestScore >= configuration.ReviewThreshold)
            return ResolutionDecision.Review;

        return ResolutionDecision.New;
    }

    private bool HasSignificantScoreChange(MatchResult original, MatchResult reprocessed)
    {
        var originalBest = original.Matches.FirstOrDefault()?.OverallScore ?? 0.0;
        var reprocessedBest = reprocessed.Matches.FirstOrDefault()?.OverallScore ?? 0.0;

        // Consider >10% change as significant
        return Math.Abs(originalBest - reprocessedBest) > 0.1;
    }

    private double CalculateSimilarityScore(MatchResult original, MatchResult reprocessed)
    {
        // Simple similarity calculation based on overlap of matches
        if (!original.Matches.Any() && !reprocessed.Matches.Any())
            return 1.0;

        var originalIds = original.Matches.Select(m => m.CandidateIdentity.Id).ToHashSet();
        var reprocessedIds = reprocessed.Matches.Select(m => m.CandidateIdentity.Id).ToHashSet();

        var intersection = originalIds.Intersect(reprocessedIds).Count();
        var union = originalIds.Union(reprocessedIds).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    private string GenerateComparisonSummary(MatchComparisonResult comparison)
    {
        var parts = new List<string>();

        if (comparison.DecisionChanged)
            parts.Add($"Decision changed from {comparison.OriginalMatch.Decision} to {comparison.ReprocessedMatch.Decision}");
        else
            parts.Add($"Decision remained {comparison.OriginalMatch.Decision}");

        parts.Add($"Similarity score: {comparison.SimilarityScore:F2}");

        if (comparison.NewMatches.Any())
            parts.Add($"Found {comparison.NewMatches.Count} new matches");

        if (comparison.LostMatches.Any())
            parts.Add($"Lost {comparison.LostMatches.Count} previous matches");

        return string.Join(". ", parts) + ".";
    }
}