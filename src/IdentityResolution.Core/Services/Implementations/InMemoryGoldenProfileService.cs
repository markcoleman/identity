using System.Collections.Concurrent;
using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.Extensions.Logging;

namespace IdentityResolution.Core.Services.Implementations;

/// <summary>
/// In-memory implementation of golden profile service for development/testing
/// </summary>
public class InMemoryGoldenProfileService : IGoldenProfileService
{
    private readonly ConcurrentDictionary<Guid, GoldenProfile> _goldenProfiles = new();
    private readonly ConcurrentDictionary<Guid, List<GoldenProfile>> _profileHistory = new();
    private readonly ConcurrentDictionary<string, Guid> _epidToPersonId = new();
    private readonly ILogger<InMemoryGoldenProfileService> _logger;

    public InMemoryGoldenProfileService(ILogger<InMemoryGoldenProfileService> logger)
    {
        _logger = logger;
    }

    public Task<GoldenProfile> CreateGoldenProfileAsync(GoldenProfile profile, CancellationToken cancellationToken = default)
    {
        // Ensure we have a valid PersonId
        if (profile.PersonId == Guid.Empty)
            profile.PersonId = Guid.NewGuid();

        // Generate EPID if not provided
        if (string.IsNullOrEmpty(profile.EPID))
            profile.EPID = $"EPID-{profile.PersonId:N}";

        profile.CreatedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.Version = 1;

        // Store the profile
        _goldenProfiles[profile.PersonId] = profile;

        // Index by EPID
        if (!string.IsNullOrEmpty(profile.EPID))
            _epidToPersonId[profile.EPID] = profile.PersonId;

        // Initialize history
        _profileHistory[profile.PersonId] = new List<GoldenProfile> { DeepCopy(profile) };

        _logger.LogInformation("Created golden profile successfully");

        return Task.FromResult(profile);
    }

    public Task<GoldenProfile?> GetGoldenProfileAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        _goldenProfiles.TryGetValue(personId, out var profile);
        return Task.FromResult(profile);
    }

    public Task<GoldenProfile?> GetGoldenProfileByEpidAsync(string epid, CancellationToken cancellationToken = default)
    {
        if (_epidToPersonId.TryGetValue(epid, out var personId))
        {
            return GetGoldenProfileAsync(personId, cancellationToken);
        }
        return Task.FromResult<GoldenProfile?>(null);
    }

    public Task<GoldenProfile> UpdateGoldenProfileAsync(GoldenProfile profile, CancellationToken cancellationToken = default)
    {
        if (!_goldenProfiles.TryGetValue(profile.PersonId, out var existingProfile))
        {
            throw new ArgumentException($"Golden profile with PersonId {profile.PersonId} not found");
        }

        // Optimistic concurrency check
        if (existingProfile.Version != profile.Version)
        {
            throw new OptimisticConcurrencyException($"Version mismatch. Expected {existingProfile.Version}, got {profile.Version}");
        }

        // Update version and timestamp
        profile.Version++;
        profile.UpdatedAt = DateTime.UtcNow;

        // Store the updated profile
        _goldenProfiles[profile.PersonId] = profile;

        // Update EPID index if changed
        if (!string.IsNullOrEmpty(profile.EPID) && profile.EPID != existingProfile.EPID)
        {
            _epidToPersonId[profile.EPID] = profile.PersonId;
            if (!string.IsNullOrEmpty(existingProfile.EPID))
                _epidToPersonId.TryRemove(existingProfile.EPID, out _);
        }

        // Add to history
        if (_profileHistory.TryGetValue(profile.PersonId, out var history))
        {
            history.Add(DeepCopy(profile));
        }

        _logger.LogInformation("Updated golden profile successfully");

        return Task.FromResult(profile);
    }

    public async Task<GoldenProfile> MergeGoldenProfilesAsync(Guid primaryPersonId, Guid secondaryPersonId, string actor, CancellationToken cancellationToken = default)
    {
        var primaryProfile = await GetGoldenProfileAsync(primaryPersonId, cancellationToken);
        var secondaryProfile = await GetGoldenProfileAsync(secondaryPersonId, cancellationToken);

        if (primaryProfile == null)
            throw new ArgumentException($"Primary golden profile {primaryPersonId} not found");

        if (secondaryProfile == null)
            throw new ArgumentException($"Secondary golden profile {secondaryPersonId} not found");

        // Merge the profiles
        var mergedProfile = MergeProfiles(primaryProfile, secondaryProfile);
        mergedProfile.Version++;
        mergedProfile.UpdatedAt = DateTime.UtcNow;

        // Mark secondary profile as merged
        secondaryProfile.Status = GoldenProfileStatus.Merged;
        secondaryProfile.UpdatedAt = DateTime.UtcNow;
        secondaryProfile.Version++;

        // Update both profiles
        _goldenProfiles[primaryPersonId] = mergedProfile;
        _goldenProfiles[secondaryPersonId] = secondaryProfile;

        // Add to histories
        if (_profileHistory.TryGetValue(primaryPersonId, out var primaryHistory))
            primaryHistory.Add(DeepCopy(mergedProfile));

        if (_profileHistory.TryGetValue(secondaryPersonId, out var secondaryHistory))
            secondaryHistory.Add(DeepCopy(secondaryProfile));

        _logger.LogInformation("Merged golden profiles successfully");

        return mergedProfile;
    }

    public async Task<List<GoldenProfile>> SplitGoldenProfileAsync(Guid personId, List<Guid> splitIdentityIds, string actor, CancellationToken cancellationToken = default)
    {
        var originalProfile = await GetGoldenProfileAsync(personId, cancellationToken);
        if (originalProfile == null)
            throw new ArgumentException($"Golden profile {personId} not found");

        var resultProfiles = new List<GoldenProfile>();

        // Create new profiles for each split identity group using Select for better performance
        var newProfiles = splitIdentityIds.Chunk(splitIdentityIds.Count / 2) // Simple split for demo
            .Select(splitGroup => new GoldenProfile
            {
                PersonId = Guid.NewGuid(),
                EPID = $"EPID-{Guid.NewGuid():N}",
                Status = GoldenProfileStatus.Active,
                SourceIdentityIds = splitGroup.ToList(),
                Confidence = originalProfile.Confidence * 0.8, // Reduced confidence after split
                Attributes = new Dictionary<string, object>(originalProfile.Attributes),
                Metadata = new Dictionary<string, object>(originalProfile.Metadata)
                {
                    ["SplitFromPersonId"] = personId,
                    ["SplitActor"] = actor,
                    ["SplitTimestamp"] = DateTime.UtcNow
                }
            })
            .ToList();

        // Create profiles and add to results
        foreach (var newProfile in newProfiles)
        {
            await CreateGoldenProfileAsync(newProfile, cancellationToken);
            resultProfiles.Add(newProfile);
        }

        // Mark original profile as split
        originalProfile.Status = GoldenProfileStatus.Split;
        originalProfile.UpdatedAt = DateTime.UtcNow;
        originalProfile.Version++;
        _goldenProfiles[personId] = originalProfile;

        if (_profileHistory.TryGetValue(personId, out var history))
            history.Add(DeepCopy(originalProfile));

        _logger.LogInformation("Split golden profile into {Count} new profiles",
            resultProfiles.Count);

        return resultProfiles;
    }

    public Task<IEnumerable<GoldenProfile>> GetGoldenProfilesAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default)
    {
        var profiles = _goldenProfiles.Values
            .Where(p => p.Status == GoldenProfileStatus.Active)
            .OrderBy(p => p.CreatedAt)
            .Skip(skip)
            .Take(take);

        return Task.FromResult(profiles);
    }

    public Task<IEnumerable<GoldenProfile>> SearchGoldenProfilesAsync(Dictionary<string, object> searchCriteria, CancellationToken cancellationToken = default)
    {
        var profiles = _goldenProfiles.Values
            .Where(p => p.Status == GoldenProfileStatus.Active)
            .Where(p => MatchesSearchCriteria(p, searchCriteria));

        return Task.FromResult(profiles);
    }

    public Task<IEnumerable<GoldenProfile>> GetGoldenProfileHistoryAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        if (_profileHistory.TryGetValue(personId, out var history))
        {
            return Task.FromResult(history.OrderBy(h => h.CreatedAt).AsEnumerable());
        }

        return Task.FromResult(Enumerable.Empty<GoldenProfile>());
    }

    private GoldenProfile MergeProfiles(GoldenProfile primary, GoldenProfile secondary)
    {
        var merged = DeepCopy(primary);

        // Merge source identity IDs
        merged.SourceIdentityIds.AddRange(secondary.SourceIdentityIds);
        merged.SourceIdentityIds = merged.SourceIdentityIds.Distinct().ToList();

        // Merge verified identifiers - use LINQ Where to filter existing ones
        var newIdentifiers = secondary.VerifiedIdentifiers
            .Where(identifier => !merged.VerifiedIdentifiers.Any(i => i.Type == identifier.Type && i.Value == identifier.Value))
            .ToList();
        
        foreach (var identifier in newIdentifiers)
        {
            merged.VerifiedIdentifiers.Add(identifier);
        }

        // Merge attributes (primary takes precedence for conflicts) - use Where for filtering
        var newAttributes = secondary.Attributes
            .Where(attr => !merged.Attributes.ContainsKey(attr.Key))
            .ToList();
            
        foreach (var attr in newAttributes)
        {
            merged.Attributes[attr.Key] = attr.Value;
        }

        // Update confidence (average of both profiles)
        merged.Confidence = (primary.Confidence + secondary.Confidence) / 2;

        // Add merge metadata
        merged.Metadata["MergeTimestamp"] = DateTime.UtcNow;
        merged.Metadata["MergedFromPersonId"] = secondary.PersonId;

        return merged;
    }

    private bool MatchesSearchCriteria(GoldenProfile profile, Dictionary<string, object> criteria)
    {
        foreach (var criterion in criteria)
        {
            if (profile.Attributes.TryGetValue(criterion.Key, out var value))
            {
                if (!value.Equals(criterion.Value))
                    return false;
            }
            else
            {
                return false; // Required attribute not found
            }
        }
        return true;
    }

    private GoldenProfile DeepCopy(GoldenProfile original)
    {
        // Simple deep copy for demo purposes - in production use proper serialization
        return new GoldenProfile
        {
            PersonId = original.PersonId,
            EPID = original.EPID,
            Status = original.Status,
            VerifiedIdentifiers = new List<Identifier>(original.VerifiedIdentifiers),
            Attributes = new Dictionary<string, object>(original.Attributes),
            CanonicalIdentity = original.CanonicalIdentity,
            SourceIdentityIds = new List<Guid>(original.SourceIdentityIds),
            Confidence = original.Confidence,
            CreatedAt = original.CreatedAt,
            UpdatedAt = original.UpdatedAt,
            Version = original.Version,
            Metadata = new Dictionary<string, object>(original.Metadata)
        };
    }
}