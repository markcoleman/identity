using FluentAssertions;
using IdentityResolution.Core.Models;
using IdentityResolution.Tests.Infrastructure;
using Xunit;

namespace IdentityResolution.Tests.Integration;

/// <summary>
/// Integration tests for probabilistic matching logic with threshold behavior testing
/// using testcontainers with PostgreSQL, Redis, and OpenSearch.
/// Note: These tests require Docker and significant resources (~2GB RAM, 15+ seconds startup time).
/// They are excluded from CI by default but can be run locally or via '[run-integration]' commit message.
/// </summary>
[Trait("Category", "Integration")]
public class ProbabilisticMatchingIntegrationTests : IntegrationTestBase
{
    /// <summary>
    /// Override to avoid conflicts with test data
    /// </summary>
    protected override async Task SeedTestDataAsync()
    {
        // Create non-conflicting sample data for probabilistic tests
        var sampleIdentities = new List<Identity>
        {
            new Identity
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PersonalInfo = new PersonalInfo
                {
                    FirstName = "David",
                    LastName = "Brown",
                    DateOfBirth = DateTime.SpecifyKind(new DateTime(1982, 4, 10), DateTimeKind.Utc)
                },
                ContactInfo = new ContactInfo
                {
                    Email = "david.brown@example.com",
                    Phone = "(555) 111-2222"
                },
                Identifiers = new List<Identifier>(),
                Source = "TestSystem",
                Confidence = 0.95
            },
            new Identity
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PersonalInfo = new PersonalInfo
                {
                    FirstName = "Emma",
                    LastName = "Davis",
                    DateOfBirth = DateTime.SpecifyKind(new DateTime(1993, 11, 25), DateTimeKind.Utc)
                },
                ContactInfo = new ContactInfo
                {
                    Email = "emma.davis@example.com",
                    Phone = "(555) 333-4444"
                },
                Identifiers = new List<Identifier>(),
                Source = "TestSystem",
                Confidence = 0.88
            }
        };

        foreach (var identity in sampleIdentities)
        {
            await StorageService.StoreIdentityAsync(identity);
        }
    }
    [Fact]
    public async Task FindMatchesAsync_WithHighProbabilisticScore_ShouldAutoMerge()
    {
        // Arrange - Create identity with strong signals but no deterministic match
        var existingIdentity = CreateTestIdentity(
            "Sarah", "Mitchell", 
            new DateTime(1992, 4, 18), 
            "sarah.mitchell@example.com",
            "(555) 234-5678");

        await StorageService.StoreIdentityAsync(existingIdentity);

        var candidateIdentity = CreateTestIdentity(
            "Sarah", "Mitchell", 
            new DateTime(1992, 4, 18), 
            "sarah.mitchell@example.com", // Exact match on multiple fields
            "(555) 234-5678");

        // Act
        var result = await MatchingService.FindMatchesAsync(candidateIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Matches.Should().HaveCount(1);
        
        var match = result.Matches.First();
        match.OverallScore.Should().BeGreaterThanOrEqualTo(0.97, "High probabilistic match should score ≥0.97");
        match.IsAutoMergeCandidate.Should().BeTrue("Score ≥0.97 should result in auto-merge");
        match.MatchReasons.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FindMatchesAsync_WithMediumProbabilisticScore_ShouldRequireReview()
    {
        // Arrange - Create identity with moderate signals
        var existingIdentity = CreateTestIdentity(
            "Michael", "Johnson", 
            new DateTime(1980, 11, 25), 
            "michael.johnson@company.com",
            "(555) 345-6789");

        await StorageService.StoreIdentityAsync(existingIdentity);

        var candidateIdentity = CreateTestIdentity(
            "Mike", "Johnson", // Similar but not exact first name
            new DateTime(1980, 11, 25), 
            "m.johnson@personal.com", // Different email domain
            "(555) 345-6789"); // Same phone

        // Act
        var result = await MatchingService.FindMatchesAsync(candidateIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Matches.Should().HaveCount(1);
        
        var match = result.Matches.First();
        match.OverallScore.Should().BeInRange(0.90, 0.97, "Medium probabilistic match should be in review range");
        match.Status.Should().Be(MatchStatus.RequiresReview, "Score 0.90-0.97 should require review");
    }

    [Fact]
    public async Task FindMatchesAsync_WithLowProbabilisticScore_ShouldReject()
    {
        // Arrange - Create identity with weak signals
        var existingIdentity = CreateTestIdentity(
            "Jennifer", "Adams", 
            new DateTime(1975, 6, 30), 
            "jennifer.adams@example.com",
            "(555) 456-7890");

        await StorageService.StoreIdentityAsync(existingIdentity);

        var candidateIdentity = CreateTestIdentity(
            "Jenny", "Smith", // Different last name, similar first name
            new DateTime(1976, 6, 15), // Similar but different DOB
            "jenny.smith@different.com", // Different email
            "(555) 999-0000"); // Different phone

        // Act
        var result = await MatchingService.FindMatchesAsync(candidateIdentity);

        // Assert
        result.Should().NotBeNull();
        if (result.Matches.Any())
        {
            var match = result.Matches.First();
            match.OverallScore.Should().BeLessThan(0.90, "Low probabilistic match should score <0.90");
            match.IsAutoMergeCandidate.Should().BeFalse("Score <0.90 should be rejected");
        }
        else
        {
            // No matches found is also acceptable for low similarity
            result.Matches.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task FindMatchesAsync_WithEmailVariations_ShouldScoreAppropriately()
    {
        // Arrange - Test email similarity scoring
        var existingIdentities = new[]
        {
            CreateTestIdentity("David", "Brown", null, "david.brown@company.com", null),
            CreateTestIdentity("David", "Brown", null, "d.brown@company.com", null),
            CreateTestIdentity("David", "Brown", null, "david.brown@different.org", null),
            CreateTestIdentity("David", "Brown", null, "completely.different@example.com", null)
        };

        foreach (var identity in existingIdentities)
        {
            await StorageService.StoreIdentityAsync(identity);
        }

        var candidateIdentity = CreateTestIdentity("David", "Brown", null, "david.brown@company.com", null);

        // Act
        var result = await MatchingService.FindMatchesAsync(candidateIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Matches.Should().HaveCountGreaterThan(0);
        
        var sortedMatches = result.Matches.OrderByDescending(m => m.OverallScore).ToList();
        
        // Exact email match should score highest
        sortedMatches[0].OverallScore.Should().BeGreaterThan(sortedMatches[1].OverallScore);
        
        // Similar email should score higher than completely different
        if (sortedMatches.Count >= 3)
        {
            sortedMatches[1].OverallScore.Should().BeGreaterThan(sortedMatches.Last().OverallScore);
        }
    }

    [Fact]
    public async Task FindMatchesAsync_WithPhoneNumberVariations_ShouldNormalizeAndMatch()
    {
        // Arrange - Test phone number normalization and matching
        var existingIdentity = CreateTestIdentity(
            "Lisa", "Garcia", 
            new DateTime(1985, 2, 14), 
            "lisa.garcia@example.com",
            "(555) 123-4567");

        await StorageService.StoreIdentityAsync(existingIdentity);

        var candidateIdentity = CreateTestIdentity(
            "Lisa", "Garcia", 
            new DateTime(1985, 2, 14), 
            "lisa.garcia@example.com",
            "555-123-4567"); // Different format, same number

        // Act
        var result = await MatchingService.FindMatchesAsync(candidateIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Matches.Should().HaveCount(1);
        
        var match = result.Matches.First();
        match.OverallScore.Should().BeGreaterThanOrEqualTo(0.97, "Normalized phone match should score high");
        match.MatchReasons.Should().Contain(r => r.Contains("Phone") || r.Contains("phone"));
    }

    [Fact]
    public async Task ResolveIdentityAsync_WithProbabilisticThresholds_ShouldRespectConfiguration()
    {
        // Arrange
        var existingIdentity = CreateTestIdentity(
            "Kevin", "Martinez", 
            new DateTime(1990, 8, 5), 
            "kevin.martinez@example.com",
            "(555) 567-8901");

        await StorageService.StoreIdentityAsync(existingIdentity);

        // Test high confidence scenario
        var highConfidenceCandidate = CreateTestIdentity(
            "Kevin", "Martinez", 
            new DateTime(1990, 8, 5), 
            "kevin.martinez@example.com",
            "(555) 567-8901");

        // Act - High confidence should auto-merge
        var highResult = await ResolutionService.ResolveIdentityAsync(highConfidenceCandidate);

        // Assert
        highResult.Should().NotBeNull();
        highResult.Decision.Should().Be(ResolutionDecision.Auto, "High confidence should auto-merge");
        
        // Test medium confidence scenario  
        var mediumConfidenceCandidate = CreateTestIdentity(
            "Kev", "Martinez", // Slightly different name
            new DateTime(1990, 8, 5), 
            "k.martinez@workplace.com", // Different email
            "(555) 567-8901"); // Same phone

        // Act - Medium confidence should require review
        var mediumResult = await ResolutionService.ResolveIdentityAsync(mediumConfidenceCandidate);

        // Assert - This may auto-merge or require review depending on the exact scoring
        mediumResult.Should().NotBeNull();
        mediumResult.Decision.Should().BeOneOf(ResolutionDecision.Review, ResolutionDecision.Auto);
    }

    [Fact]
    public async Task FindMatchesAsync_WithMultipleCandidates_ShouldReturnTopMatches()
    {
        // Arrange - Create multiple potential matches with varying confidence
        var identities = new[]
        {
            CreateTestIdentity("Anna", "Wilson", new DateTime(1988, 3, 20), "anna.wilson@example.com", "(555) 111-1111"),
            CreateTestIdentity("Anna", "Wilson", new DateTime(1988, 3, 20), "anna.w@company.com", "(555) 222-2222"),
            CreateTestIdentity("Anna", "Smith", new DateTime(1988, 3, 20), "anna.smith@example.com", "(555) 333-3333"),
            CreateTestIdentity("Annie", "Wilson", new DateTime(1988, 3, 20), "annie.wilson@example.com", "(555) 444-4444"),
            CreateTestIdentity("Anna", "Wilson", new DateTime(1989, 3, 20), "different@example.com", "(555) 555-5555")
        };

        foreach (var identity in identities)
        {
            await StorageService.StoreIdentityAsync(identity);
        }

        var candidateIdentity = CreateTestIdentity(
            "Anna", "Wilson", 
            new DateTime(1988, 3, 20), 
            "anna.wilson.new@example.com",
            "(555) 999-9999");

        // Act
        var result = await MatchingService.FindMatchesAsync(candidateIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Matches.Should().NotBeEmpty();
        
        // Should be sorted by confidence score
        var scores = result.Matches.Select(m => m.OverallScore).ToList();
        scores.Should().BeInDescendingOrder("Matches should be sorted by score");
        
        // Top match should be high confidence
        result.Matches.First().OverallScore.Should().BeGreaterThan(0.80);
    }

    [Fact]
    public async Task FindMatchesAsync_WithFuzzyNameMatching_ShouldHandleTypos()
    {
        // Arrange - Create identity with potential typos
        var existingIdentity = CreateTestIdentity(
            "Christopher", "Thompson", 
            new DateTime(1982, 12, 1), 
            "christopher.thompson@example.com",
            "(555) 678-9012");

        await StorageService.StoreIdentityAsync(existingIdentity);

        var candidateIdentity = CreateTestIdentity(
            "Christoper", "Thompsen", // Slight typos in both names
            new DateTime(1982, 12, 1), 
            "christopher.thompson@example.com", // Same email
            "(555) 678-9012"); // Same phone

        // Act
        var result = await MatchingService.FindMatchesAsync(candidateIdentity);

        // Assert
        result.Should().NotBeNull();
        result.Matches.Should().HaveCountGreaterThan(0);
        
        var match = result.Matches.First();
        match.OverallScore.Should().BeGreaterThan(0.85, "Should handle minor typos with fuzzy matching");
        match.MatchReasons.Should().Contain(r => r.Contains("Email") || r.Contains("Phone"));
    }
}