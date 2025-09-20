using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.Extensions.Logging;

namespace IdentityResolution.Core.Services.Implementations;

/// <summary>
/// In-memory implementation of identity storage service
/// </summary>
public class InMemoryIdentityStorageService : IIdentityStorageService
{
    private readonly Dictionary<Guid, Identity> _identities = new();
    private readonly ILogger<InMemoryIdentityStorageService> _logger;

    public InMemoryIdentityStorageService(ILogger<InMemoryIdentityStorageService> logger)
    {
        _logger = logger;
    }

    public Task<Identity> StoreIdentityAsync(Identity identity, CancellationToken cancellationToken = default)
    {
        if (identity.Id == Guid.Empty)
        {
            identity.Id = Guid.NewGuid();
        }

        identity.CreatedAt = DateTime.UtcNow;
        identity.UpdatedAt = DateTime.UtcNow;

        _identities[identity.Id] = identity;

        _logger.LogInformation("Stored identity {IdentityId}", identity.Id);

        return Task.FromResult(identity);
    }

    public Task<Identity?> GetIdentityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _identities.TryGetValue(id, out var identity);
        return Task.FromResult(identity);
    }

    public Task<IEnumerable<Identity>> GetAllIdentitiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<Identity>>(_identities.Values);
    }

    public Task<Identity> UpdateIdentityAsync(Identity identity, CancellationToken cancellationToken = default)
    {
        if (!_identities.ContainsKey(identity.Id))
        {
            throw new ArgumentException($"Identity with ID {identity.Id} not found", nameof(identity));
        }

        identity.UpdatedAt = DateTime.UtcNow;
        _identities[identity.Id] = identity;

        _logger.LogInformation("Updated identity {IdentityId}", identity.Id);

        return Task.FromResult(identity);
    }

    public Task<bool> DeleteIdentityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var removed = _identities.Remove(id);

        if (removed)
        {
            _logger.LogInformation("Deleted identity {IdentityId}", id);
        }

        return Task.FromResult(removed);
    }
}
