using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IdentityResolution.Tests.Infrastructure;

/// <summary>
/// PostgreSQL implementation of identity storage service for integration testing
/// </summary>
public class PostgreSqlIdentityStorageService : IIdentityStorageService
{
    private readonly IdentityDbContext _context;
    private readonly ILogger<PostgreSqlIdentityStorageService> _logger;

    public PostgreSqlIdentityStorageService(IdentityDbContext context, ILogger<PostgreSqlIdentityStorageService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Identity> StoreIdentityAsync(Identity identity, CancellationToken cancellationToken = default)
    {
        if (identity.Id == Guid.Empty)
        {
            identity.Id = Guid.NewGuid();
        }

        identity.CreatedAt = DateTime.UtcNow;
        identity.UpdatedAt = DateTime.UtcNow;

        _context.Identities.Add(identity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stored identity {IdentityId} in PostgreSQL", identity.Id);

        return identity;
    }

    public async Task<Identity?> GetIdentityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Identities.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Identity>> GetAllIdentitiesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Identities.ToListAsync(cancellationToken);
    }

    public async Task<Identity> UpdateIdentityAsync(Identity identity, CancellationToken cancellationToken = default)
    {
        var existingIdentity = await _context.Identities.FindAsync(new object[] { identity.Id }, cancellationToken);
        if (existingIdentity == null)
        {
            throw new InvalidOperationException($"Identity with ID {identity.Id} not found");
        }

        identity.UpdatedAt = DateTime.UtcNow;
        _context.Entry(existingIdentity).CurrentValues.SetValues(identity);

        // Handle complex properties
        existingIdentity.PersonalInfo = identity.PersonalInfo;
        existingIdentity.ContactInfo = identity.ContactInfo;
        existingIdentity.Identifiers = identity.Identifiers;
        existingIdentity.Attributes = identity.Attributes;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated identity {IdentityId} in PostgreSQL", identity.Id);

        return existingIdentity;
    }

    public async Task<bool> DeleteIdentityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var identity = await _context.Identities.FindAsync(new object[] { id }, cancellationToken);
        if (identity == null)
        {
            return false;
        }

        _context.Identities.Remove(identity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted identity {IdentityId} from PostgreSQL", id);

        return true;
    }
}