using FluentAssertions;
using IdentityResolution.Core.Models;
using IdentityResolution.Tests.Infrastructure;
using Xunit;

namespace IdentityResolution.Tests.Integration;

/// <summary>
/// Integration tests for deterministic matching logic (SSN + DOB exact matches)
/// using testcontainers with PostgreSQL, Redis, and OpenSearch.
/// Note: These tests require Docker and significant resources (~2GB RAM, 15+ seconds startup time).
/// They are excluded from CI by default but can be run locally or via '[run-integration]' commit message.
/// </summary>
[Trait("Category", "Integration")]
public class DeterministicMatchingIntegrationTests : IntegrationTestBase
{
    /// <summary>
    /// Override to avoid conflicts with test data
    /// </summary>
    protected override async Task SeedTestDataAsync()
    {
        // Create non-conflicting sample data for deterministic tests
        var sampleIdentities = new List<Identity>
        {
            // Different test data to avoid conflicts with test cases
            new Identity
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PersonalInfo = new PersonalInfo
                {
                    FirstName = "Alice",
                    LastName = "Johnson",
                    DateOfBirth = DateTime.SpecifyKind(new DateTime(1990, 7, 22), DateTimeKind.Utc)
                },
                ContactInfo = new ContactInfo
                {
                    Email = "alice.johnson@example.com",
                    Phone = "(555) 987-6543"
                },
                Identifiers = new List<Identifier>
                {
                    new Identifier
                    {
                        Type = IdentifierTypes.SocialSecurityNumber,
                        Value = "987-65-4321"
                    }
                },
                Source = "TestSystem",
                Confidence = 1.0
            },
            new Identity
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PersonalInfo = new PersonalInfo
                {
                    FirstName = "Bob",
                    LastName = "Wilson",
                    DateOfBirth = DateTime.SpecifyKind(new DateTime(1975, 12, 8), DateTimeKind.Utc)
                },
                ContactInfo = new ContactInfo
                {
                    Email = "bob.wilson@example.com",
                    Phone = "(555) 456-7890"
                },
                Identifiers = new List<Identifier>(),
                Source = "TestSystem",
                Confidence = 0.85
            }
        };

        foreach (var identity in sampleIdentities)
        {
            await StorageService.StoreIdentityAsync(identity);
        }
    }
    [Fact]
    public async Task FindMatchesAsync_WithExactSSNAndDOBMatch_ShouldReturnPerfectScore()
    {
        // Arrange
        var existingIdentity = CreateTestIdentity(
            "John", "Smith",
            new DateTime(1985, 3, 15),
            "john.smith@example.com",
            "(555) 123-4567",
            "123-45-6789");

        var candidateIdentity = CreateTestIdentity(
            "John", "Smith",
            new DateTime(1985, 3, 15),
            "john.smith.work@company.com", // Different email 
            "(555) 123-4567",
            "123-45-6789"); // Same SSN

        await StorageService.StoreIdentityAsync(existingIdentity);

        // Act
        var result = await MatchingService.FindMatchesAsync(candidateIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Matches.Should().HaveCount(1);

        var match = result.Matches.First();
        match.OverallScore.Should().BeGreaterThanOrEqualTo(0.97, "SSN + DOB exact match should have high confidence");
        match.IsAutoMergeCandidate.Should().BeTrue("High confidence matches should auto-merge");
        match.MatchReasons.Should().Contain(r => r.Contains("SSN"));
        match.MatchReasons.Should().Contain(r => r.Contains("DateOfBirth"));
    }

    [Fact]
    public async Task FindMatchesAsync_WithSSNMatchButDifferentDOB_ShouldRequireReview()
    {
        // Arrange
        var existingIdentity = CreateTestIdentity(
            "John", "Smith",
            new DateTime(1985, 3, 15),
            "john.smith@example.com",
            "(555) 123-4567",
            "123-45-6789");

        var candidateIdentity = CreateTestIdentity(
            "John", "Smith",
            new DateTime(1986, 3, 15), // Different year
            "john.smith@example.com",
            "(555) 123-4567",
            "123-45-6789"); // Same SSN

        await StorageService.StoreIdentityAsync(existingIdentity);

        // Act
        var result = await MatchingService.FindMatchesAsync(candidateIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Matches.Should().HaveCount(1);

        var match = result.Matches.First();
        match.OverallScore.Should().BeInRange(0.90, 0.97, "SSN match with DOB conflict should require review");
        match.Status.Should().Be(MatchStatus.RequiresReview, "Conflicting data should require manual review");
        match.MatchReasons.Should().Contain(r => r.Contains("SSN"));
        match.MatchReasons.Should().Contain(r => r.Contains("conflict") || r.Contains("mismatch"));
    }

    [Fact]
    public async Task FindMatchesAsync_WithMultipleSSNMatches_ShouldRankByAdditionalFactors()
    {
        // Arrange - Create multiple identities with same SSN but different additional data
        var baseSSN = "555-66-7777";

        var identity1 = CreateTestIdentity(
            "Alice", "Johnson",
            new DateTime(1990, 5, 20),
            "alice.johnson@example.com",
            "(555) 111-2222",
            baseSSN);

        var identity2 = CreateTestIdentity(
            "Alice", "Johnson",
            new DateTime(1990, 5, 20),
            "alice.j@workplace.com", // Different email but same name/DOB
            "(555) 333-4444",
            baseSSN);

        var identity3 = CreateTestIdentity(
            "Alice", "Smith", // Different last name
            new DateTime(1990, 5, 20),
            "alice.smith@example.com",
            "(555) 555-6666",
            baseSSN);

        await StorageService.StoreIdentityAsync(identity1);
        await StorageService.StoreIdentityAsync(identity2);
        await StorageService.StoreIdentityAsync(identity3);

        var candidateIdentity = CreateTestIdentity(
            "Alice", "Johnson",
            new DateTime(1990, 5, 20),
            "alice.johnson.new@example.com",
            "(555) 777-8888",
            baseSSN);

        // Act
        var result = await MatchingService.FindMatchesAsync(candidateIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Matches.Should().HaveCount(3);

        // Matches should be sorted by score (highest first)
        var sortedMatches = result.Matches.OrderByDescending(m => m.OverallScore).ToList();

        // The exact name match should score highest
        sortedMatches[0].OverallScore.Should().BeGreaterThan(sortedMatches[1].OverallScore);
        sortedMatches[1].OverallScore.Should().BeGreaterThan(sortedMatches[2].OverallScore);

        // All should be high confidence due to SSN + DOB match
        sortedMatches.All(m => m.OverallScore >= 0.90).Should().BeTrue();
    }

    [Fact]
    public async Task ResolveIdentityAsync_WithDeterministicMatch_ShouldAutoMerge()
    {
        // Arrange
        var existingIdentity = CreateTestIdentity(
            "Bob", "Wilson",
            new DateTime(1975, 12, 8),
            "bob.wilson@example.com",
            "(555) 456-7890",
            "987-65-4321");

        await StorageService.StoreIdentityAsync(existingIdentity);

        var newIdentity = CreateTestIdentity(
            "Robert", "Wilson", // Slight name variation
            new DateTime(1975, 12, 8),
            "robert.wilson@workplace.com",
            "(555) 456-7890",
            "987-65-4321"); // Same SSN

        // Act
        var result = await ResolutionService.ResolveIdentityAsync(newIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Decision.Should().Be(ResolutionDecision.Auto);
        result.WasAutoMerged.Should().BeTrue();
        result.ResolvedIdentity.Should().NotBeNull();

        // Should have combined information from both identities
        result.ResolvedIdentity.PersonalInfo.FirstName.Should().NotBeNullOrEmpty();
        result.ResolvedIdentity.ContactInfo.Email.Should().NotBeNullOrEmpty();

        // Audit data should be present
        result.AuditData.Should().ContainKey("algorithm");
        result.AuditData.Should().ContainKey("matchCount");
        result.AuditData.Should().ContainKey("thresholds");
    }

    [Fact]
    public async Task ResolveIdentityAsync_WithSSNConflict_ShouldRequireReview()
    {
        // Arrange
        var existingIdentity = CreateTestIdentity(
            "Carol", "Davis",
            new DateTime(1988, 9, 12),
            "carol.davis@example.com",
            "(555) 789-0123",
            "111-22-3333");

        await StorageService.StoreIdentityAsync(existingIdentity);

        var newIdentity = CreateTestIdentity(
            "Carol", "Davis",
            new DateTime(1987, 9, 12), // Different year - potential data error
            "carol.davis@example.com",
            "(555) 789-0123",
            "111-22-3333"); // Same SSN

        // Act
        var result = await ResolutionService.ResolveIdentityAsync(newIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Decision.Should().Be(ResolutionDecision.Review);
        result.Warnings.Should().NotBeEmpty();
        result.Warnings.Should().Contain(item => item.Contains("DateOfBirth"));
    }

    [Fact]
    public void TokenizationService_WithSSNMatching_ShouldMaintainConsistency()
    {
        // Arrange
        var ssn1 = "123-45-6789";
        var ssn2 = "123456789"; // Same SSN, different format
        var ssn3 = "123-45-6790"; // Different SSN

        // Act
        var token1 = TokenizationService.TokenizeSSN(ssn1);
        var token2 = TokenizationService.TokenizeSSN(ssn2);
        var token3 = TokenizationService.TokenizeSSN(ssn3);

        // Assert
        token1.Should().Be(token2, "Same SSN in different formats should produce same token");
        token1.Should().NotBe(token3, "Different SSNs should produce different tokens");

        TokenizationService.ValidateSSNToken(ssn1, token1).Should().BeTrue();
        TokenizationService.ValidateSSNToken(ssn2, token1).Should().BeTrue();
        TokenizationService.ValidateSSNToken(ssn3, token1).Should().BeFalse();
    }
}
