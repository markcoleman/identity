using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IdentityResolution.Tests.Core.Services;

public class InMemoryIdentityStorageServiceTests
{
    private readonly InMemoryIdentityStorageService _service;

    public InMemoryIdentityStorageServiceTests()
    {
        _service = new InMemoryIdentityStorageService(NullLogger<InMemoryIdentityStorageService>.Instance);
    }

    [Fact]
    public async Task StoreIdentityAsync_ShouldGenerateIdIfEmpty()
    {
        // Arrange
        var identity = new Identity
        {
            PersonalInfo = new PersonalInfo
            {
                FirstName = "John",
                LastName = "Doe"
            }
        };

        // Act
        var result = await _service.StoreIdentityAsync(identity);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.True(result.CreatedAt > DateTime.MinValue);
        Assert.True(result.UpdatedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task GetIdentityAsync_WithExistingId_ShouldReturnIdentity()
    {
        // Arrange
        var identity = new Identity
        {
            PersonalInfo = new PersonalInfo
            {
                FirstName = "John",
                LastName = "Doe"
            }
        };
        var stored = await _service.StoreIdentityAsync(identity);

        // Act
        var result = await _service.GetIdentityAsync(stored.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal("John", result.PersonalInfo.FirstName);
    }

    [Fact]
    public async Task GetIdentityAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service.GetIdentityAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllIdentitiesAsync_ShouldReturnAllStoredIdentities()
    {
        // Arrange
        var identity1 = new Identity { PersonalInfo = new PersonalInfo { FirstName = "John" } };
        var identity2 = new Identity { PersonalInfo = new PersonalInfo { FirstName = "Jane" } };

        await _service.StoreIdentityAsync(identity1);
        await _service.StoreIdentityAsync(identity2);

        // Act
        var result = await _service.GetAllIdentitiesAsync();

        // Assert
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task DeleteIdentityAsync_WithExistingId_ShouldReturnTrue()
    {
        // Arrange
        var identity = new Identity { PersonalInfo = new PersonalInfo { FirstName = "John" } };
        var stored = await _service.StoreIdentityAsync(identity);

        // Act
        var result = await _service.DeleteIdentityAsync(stored.Id);

        // Assert
        Assert.True(result);

        // Verify it's actually deleted
        var retrieved = await _service.GetIdentityAsync(stored.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteIdentityAsync_WithNonExistentId_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service.DeleteIdentityAsync(nonExistentId);

        // Assert
        Assert.False(result);
    }
}
