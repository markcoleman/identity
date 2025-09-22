using FluentAssertions;
using IdentityResolution.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdentityResolution.Tests.Integration;

/// <summary>
/// Basic integration tests to verify testcontainers setup and database connectivity.
/// Note: These tests require Docker and significant resources (~2GB RAM, 15+ seconds startup time).
/// They are excluded from CI by default but can be run locally or via '[run-integration]' commit message.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Integration")]
public class BasicContainerIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task Database_ShouldBeCreatedAndAccessible()
    {
        // Act & Assert
        var canConnect = await DbContext.Database.CanConnectAsync();
        canConnect.Should().BeTrue("Database should be accessible");

        var identityCount = await DbContext.Identities.CountAsync();
        identityCount.Should().BeGreaterThanOrEqualTo(0, "Should be able to query identities table");
    }

    [Fact]
    public async Task StorageService_ShouldStoreAndRetrieveIdentity()
    {
        // Arrange
        var testIdentity = CreateTestIdentity(
            "Test", "User",
            new DateTime(1990, 1, 1),
            "test.user@example.com",
            "(555) 123-4567");

        // Act
        var storedIdentity = await StorageService.StoreIdentityAsync(testIdentity);
        var retrievedIdentity = await StorageService.GetIdentityAsync(storedIdentity.Id);

        // Assert
        storedIdentity.Should().NotBeNull();
        storedIdentity.Id.Should().NotBe(Guid.Empty);

        retrievedIdentity.Should().NotBeNull();
        retrievedIdentity!.PersonalInfo.FirstName.Should().Be("Test");
        retrievedIdentity.PersonalInfo.LastName.Should().Be("User");
        retrievedIdentity.ContactInfo.Email.Should().Be("test.user@example.com");
    }

    [Fact]
    public async Task StorageService_ShouldHandleMultipleIdentities()
    {
        // Arrange
        var identities = new[]
        {
            CreateTestIdentity("Alice", "Smith", new DateTime(1985, 5, 15), "alice@example.com"),
            CreateTestIdentity("Bob", "Jones", new DateTime(1990, 10, 20), "bob@example.com"),
            CreateTestIdentity("Carol", "Davis", new DateTime(1988, 3, 8), "carol@example.com")
        };

        // Act
        foreach (var identity in identities)
        {
            await StorageService.StoreIdentityAsync(identity);
        }

        var allIdentities = await StorageService.GetAllIdentitiesAsync();

        // Assert
        allIdentities.Should().NotBeEmpty();
        allIdentities.Should().HaveCountGreaterThanOrEqualTo(3, "Should contain at least the 3 test identities plus any seed data");

        var testIdentities = allIdentities.Where(i =>
            i.PersonalInfo.FirstName == "Alice" ||
            i.PersonalInfo.FirstName == "Bob" ||
            i.PersonalInfo.FirstName == "Carol").ToList();

        testIdentities.Should().HaveCount(3, "All test identities should be stored");
    }

    [Fact]
    public void NormalizationService_ShouldNormalizeIdentityData()
    {
        // Arrange
        var unnormalizedIdentity = CreateTestIdentity(
            "  john  ", // Extra spaces
            "  SMITH  ", // Uppercase with spaces
            new DateTime(1980, 12, 25),
            "John.Smith@EXAMPLE.COM", // Mixed case email
            "555.123.4567"); // Phone with dots

        // Act
        var normalizedIdentity = NormalizationService.NormalizeIdentity(unnormalizedIdentity);

        // Assert
        normalizedIdentity.PersonalInfo.FirstName.Should().Be("John", "First name should be normalized");
        normalizedIdentity.PersonalInfo.LastName.Should().Be("Smith", "Last name should be normalized");
        normalizedIdentity.ContactInfo.Email.Should().Be("john.smith@example.com", "Email should be lowercase");
        normalizedIdentity.ContactInfo.Phone.Should().Be("(555) 123-4567", "Phone should be formatted");
    }

    [Fact]
    public void TokenizationService_ShouldCreateConsistentTokens()
    {
        // Arrange
        var ssn1 = "123-45-6789";
        var ssn2 = "123456789"; // Same SSN, different format

        // Act
        var token1 = TokenizationService.TokenizeSSN(ssn1);
        var token2 = TokenizationService.TokenizeSSN(ssn2);

        // Assert
        token1.Should().NotBeNullOrEmpty("Token should not be empty");
        token2.Should().NotBeNullOrEmpty("Token should not be empty");
        token1.Should().Be(token2, "Same SSN in different formats should produce identical tokens");

        TokenizationService.ValidateSSNToken(ssn1, token1).Should().BeTrue();
        TokenizationService.ValidateSSNToken(ssn2, token1).Should().BeTrue();
    }

    [Fact]
    public async Task MatchingService_WithDatabaseData_ShouldFindExactMatches()
    {
        // Arrange - Store a test identity with SSN
        var existingIdentity = CreateTestIdentity(
            "Database", "TestUser",
            DateTime.SpecifyKind(new DateTime(1990, 6, 15), DateTimeKind.Utc),
            "database.test@example.com",
            "(555) 999-8888",
            "999-88-7777");

        await StorageService.StoreIdentityAsync(existingIdentity);

        // Create a candidate with same SSN and DOB
        var candidateIdentity = CreateTestIdentity(
            "Database", "TestUser",
            DateTime.SpecifyKind(new DateTime(1990, 6, 15), DateTimeKind.Utc),
            "different.email@example.com", // Different email
            "(555) 888-9999", // Different phone
            "999-88-7777"); // Same SSN

        // Act
        var result = await MatchingService.FindMatchesAsync(candidateIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Matches.Should().HaveCountGreaterThan(0, "Should find the matching identity");

        var bestMatch = result.Matches.OrderByDescending(m => m.OverallScore).First();
        bestMatch.OverallScore.Should().BeGreaterThan(0.9, "SSN match should have high confidence");
        bestMatch.CandidateIdentity.Id.Should().Be(existingIdentity.Id);
    }
}
