using IdentityResolution.Core.Models;

namespace IdentityResolution.Core.Services;

/// <summary>
/// Service for matching identities against a collection of candidates
/// </summary>
public interface IIdentityMatchingService
{
    /// <summary>
    /// Find potential matches for a given identity
    /// </summary>
    /// <param name="identity">The identity to find matches for</param>
    /// <param name="configuration">Matching configuration and thresholds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A match result containing potential matches</returns>
    Task<MatchResult> FindMatchesAsync(Identity identity, MatchingConfiguration? configuration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compare two specific identities and return a match score
    /// </summary>
    /// <param name="identity1">First identity</param>
    /// <param name="identity2">Second identity</param>
    /// <param name="configuration">Matching configuration</param>
    /// <returns>An identity match with score details</returns>
    IdentityMatch CompareIdentities(Identity identity1, Identity identity2, MatchingConfiguration? configuration = null);
}

/// <summary>
/// Service for managing identity storage and retrieval
/// </summary>
public interface IIdentityStorageService
{
    /// <summary>
    /// Store a new identity
    /// </summary>
    /// <param name="identity">The identity to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The stored identity with any generated IDs</returns>
    Task<Identity> StoreIdentityAsync(Identity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve an identity by ID
    /// </summary>
    /// <param name="id">The identity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The identity or null if not found</returns>
    Task<Identity?> GetIdentityAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all identities (for matching purposes)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All stored identities</returns>
    Task<IEnumerable<Identity>> GetAllIdentitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing identity
    /// </summary>
    /// <param name="identity">The identity to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated identity</returns>
    Task<Identity> UpdateIdentityAsync(Identity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an identity
    /// </summary>
    /// <param name="id">The identity ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteIdentityAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for resolving and merging matched identities
/// </summary>
public interface IIdentityResolutionService
{
    /// <summary>
    /// Resolve an identity by finding matches and potentially merging
    /// </summary>
    /// <param name="identity">The identity to resolve</param>
    /// <param name="configuration">Matching and resolution configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Resolution result with merged identity and match information</returns>
    Task<ResolutionResult> ResolveIdentityAsync(Identity identity, MatchingConfiguration? configuration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merge two identities into one
    /// </summary>
    /// <param name="primaryIdentity">The primary identity to keep</param>
    /// <param name="secondaryIdentity">The secondary identity to merge in</param>
    /// <returns>The merged identity</returns>
    Identity MergeIdentities(Identity primaryIdentity, Identity secondaryIdentity);
}

/// <summary>
/// Service for data normalization and cleaning
/// </summary>
public interface IDataNormalizationService
{
    /// <summary>
    /// Normalize an identity record
    /// </summary>
    /// <param name="identity">The identity to normalize</param>
    /// <returns>The normalized identity</returns>
    Identity NormalizeIdentity(Identity identity);

    /// <summary>
    /// Normalize a name field
    /// </summary>
    /// <param name="name">The name to normalize</param>
    /// <returns>The normalized name</returns>
    string NormalizeName(string? name);

    /// <summary>
    /// Normalize an email address
    /// </summary>
    /// <param name="email">The email to normalize</param>
    /// <returns>The normalized email</returns>
    string NormalizeEmail(string? email);

    /// <summary>
    /// Normalize a phone number
    /// </summary>
    /// <param name="phone">The phone number to normalize</param>
    /// <returns>The normalized phone number</returns>
    string NormalizePhone(string? phone);

    /// <summary>
    /// Normalize and tokenize an address into components
    /// </summary>
    /// <param name="address">The address to normalize</param>
    /// <returns>The normalized address with tokenized components</returns>
    AddressTokens NormalizeAddress(Address? address);
}

/// <summary>
/// Service for tokenizing sensitive identifiers like SSN
/// </summary>
public interface ITokenizationService
{
    /// <summary>
    /// Generate a deterministic token for an SSN
    /// </summary>
    /// <param name="ssn">The SSN to tokenize</param>
    /// <returns>A deterministic token that can be used for matching</returns>
    string TokenizeSSN(string ssn);

    /// <summary>
    /// Validate if a token matches an SSN
    /// </summary>
    /// <param name="ssn">The SSN to validate</param>
    /// <param name="token">The token to validate against</param>
    /// <returns>True if the token matches the SSN</returns>
    bool ValidateSSNToken(string ssn, string token);
}




