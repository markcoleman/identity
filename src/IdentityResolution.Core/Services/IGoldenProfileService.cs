using IdentityResolution.Core.Models;

namespace IdentityResolution.Core.Services;

/// <summary>
/// Service for managing golden profiles (consolidated person records)
/// </summary>
public interface IGoldenProfileService
{
    /// <summary>
    /// Create a new golden profile
    /// </summary>
    /// <param name="profile">The golden profile to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created golden profile</returns>
    Task<GoldenProfile> CreateGoldenProfileAsync(GoldenProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get golden profile by person ID
    /// </summary>
    /// <param name="personId">The person ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The golden profile or null if not found</returns>
    Task<GoldenProfile?> GetGoldenProfileAsync(Guid personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get golden profile by EPID
    /// </summary>
    /// <param name="epid">The enterprise person ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The golden profile or null if not found</returns>
    Task<GoldenProfile?> GetGoldenProfileByEpidAsync(string epid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a golden profile (transaction-safe)
    /// </summary>
    /// <param name="profile">The updated golden profile</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated golden profile</returns>
    /// <exception cref="OptimisticConcurrencyException">Thrown when version mismatch occurs</exception>
    Task<GoldenProfile> UpdateGoldenProfileAsync(GoldenProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge golden profiles
    /// </summary>
    /// <param name="primaryPersonId">The primary person ID to keep</param>
    /// <param name="secondaryPersonId">The secondary person ID to merge</param>
    /// <param name="actor">The user/system performing the merge</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The merged golden profile</returns>
    Task<GoldenProfile> MergeGoldenProfilesAsync(Guid primaryPersonId, Guid secondaryPersonId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Split a golden profile into multiple profiles
    /// </summary>
    /// <param name="personId">The person ID to split</param>
    /// <param name="splitIdentityIds">The identity IDs to separate</param>
    /// <param name="actor">The user/system performing the split</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The list of resulting golden profiles</returns>
    Task<List<GoldenProfile>> SplitGoldenProfileAsync(Guid personId, List<Guid> splitIdentityIds, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all golden profiles (with pagination)
    /// </summary>
    /// <param name="skip">Number of records to skip</param>
    /// <param name="take">Number of records to take</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of golden profiles</returns>
    Task<IEnumerable<GoldenProfile>> GetGoldenProfilesAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search golden profiles by attributes
    /// </summary>
    /// <param name="searchCriteria">Search criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching golden profiles</returns>
    Task<IEnumerable<GoldenProfile>> SearchGoldenProfilesAsync(Dictionary<string, object> searchCriteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get historical versions of a golden profile
    /// </summary>
    /// <param name="personId">The person ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Historical versions of the golden profile</returns>
    Task<IEnumerable<GoldenProfile>> GetGoldenProfileHistoryAsync(Guid personId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exception thrown when optimistic concurrency control detects a conflict
/// </summary>
public class OptimisticConcurrencyException : Exception
{
    public OptimisticConcurrencyException(string message) : base(message) { }
    public OptimisticConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}