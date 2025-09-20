using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using IdentityResolution.Core.Services.Implementations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IdentityResolution.Tests.Core.Services;

public class IdentityResolutionServiceTests
{
    private readonly Mock<IIdentityMatchingService> _mockMatchingService;
    private readonly Mock<IIdentityStorageService> _mockStorageService;
    private readonly Mock<IDataNormalizationService> _mockNormalizationService;
    private readonly IdentityResolutionService _service;

    public IdentityResolutionServiceTests()
    {
        _mockMatchingService = new Mock<IIdentityMatchingService>();
        _mockStorageService = new Mock<IIdentityStorageService>();
        _mockNormalizationService = new Mock<IDataNormalizationService>();
        var mockLogger = new Mock<ILogger<IdentityResolutionService>>();

        _service = new IdentityResolutionService(
            _mockMatchingService.Object,
            _mockStorageService.Object,
            _mockNormalizationService.Object,
            mockLogger.Object,
            null, // No audit service for tests
            null  // No review queue service for tests
        );
    }

    [Fact]
    public async Task ResolveIdentityAsync_WithNoMatches_ShouldCreateNewIdentity()
    {
        // Arrange
        var identity = CreateTestIdentity("John", "Doe");
        var normalizedIdentity = CreateTestIdentity("John", "Doe");
        var matchResult = new MatchResult
        {
            SourceIdentity = normalizedIdentity,
            Matches = new List<IdentityMatch>()
        };

        _mockNormalizationService.Setup(x => x.NormalizeIdentity(identity))
            .Returns(normalizedIdentity);
        _mockMatchingService.Setup(x => x.FindMatchesAsync(normalizedIdentity, It.IsAny<MatchingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchResult);
        _mockStorageService.Setup(x => x.StoreIdentityAsync(normalizedIdentity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedIdentity);
        _mockStorageService.Setup(x => x.UpdateIdentityAsync(It.IsAny<Identity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Identity identity, CancellationToken _) => identity);

        // Act
        var result = await _service.ResolveIdentityAsync(identity);

        // Assert
        Assert.Equal(ResolutionDecision.New, result.Decision);
        Assert.Equal(normalizedIdentity, result.ResolvedIdentity);
        Assert.False(result.WasAutoMerged);
        Assert.Contains("No potential matches found", result.Explanation);
    }

    [Fact]
    public async Task ResolveIdentityAsync_WithHighConfidenceMatch_ShouldAutoMerge()
    {
        // Arrange
        var identity = CreateTestIdentity("John", "Doe");
        var normalizedIdentity = CreateTestIdentity("John", "Doe");
        var candidateIdentity = CreateTestIdentity("John", "Doe");

        var highConfidenceMatch = new IdentityMatch
        {
            SourceIdentity = normalizedIdentity,
            CandidateIdentity = candidateIdentity,
            OverallScore = 0.98 // Above auto-merge threshold
        };

        var matchResult = new MatchResult
        {
            SourceIdentity = normalizedIdentity,
            Matches = new List<IdentityMatch> { highConfidenceMatch }
        };

        var mergedIdentity = CreateTestIdentity("John", "Doe");

        _mockNormalizationService.Setup(x => x.NormalizeIdentity(identity))
            .Returns(normalizedIdentity);
        _mockMatchingService.Setup(x => x.FindMatchesAsync(normalizedIdentity, It.IsAny<MatchingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchResult);
        _mockStorageService.Setup(x => x.UpdateIdentityAsync(It.IsAny<Identity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mergedIdentity);

        var config = new MatchingConfiguration
        {
            AutoMergeThreshold = 0.97,
            ReviewThreshold = 0.90
        };

        // Act
        var result = await _service.ResolveIdentityAsync(identity, config);

        // Assert
        Assert.Equal(ResolutionDecision.Auto, result.Decision);
        Assert.True(result.WasAutoMerged);
        Assert.Contains(normalizedIdentity, result.MergedIdentities);
        Assert.Contains("High confidence match found", result.Explanation);
    }

    [Fact]
    public async Task ResolveIdentityAsync_WithMediumConfidenceMatch_ShouldRequireReview()
    {
        // Arrange
        var identity = CreateTestIdentity("John", "Doe");
        var normalizedIdentity = CreateTestIdentity("John", "Doe");
        var candidateIdentity = CreateTestIdentity("Jon", "Doe");

        var mediumConfidenceMatch = new IdentityMatch
        {
            SourceIdentity = normalizedIdentity,
            CandidateIdentity = candidateIdentity,
            OverallScore = 0.93 // Between review and auto-merge threshold
        };

        var matchResult = new MatchResult
        {
            SourceIdentity = normalizedIdentity,
            Matches = new List<IdentityMatch> { mediumConfidenceMatch }
        };

        _mockNormalizationService.Setup(x => x.NormalizeIdentity(identity))
            .Returns(normalizedIdentity);
        _mockMatchingService.Setup(x => x.FindMatchesAsync(normalizedIdentity, It.IsAny<MatchingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchResult);
        _mockStorageService.Setup(x => x.StoreIdentityAsync(normalizedIdentity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedIdentity);
        _mockStorageService.Setup(x => x.UpdateIdentityAsync(It.IsAny<Identity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Identity identity, CancellationToken _) => identity);

        var config = new MatchingConfiguration
        {
            AutoMergeThreshold = 0.97,
            ReviewThreshold = 0.90
        };

        // Act
        var result = await _service.ResolveIdentityAsync(identity, config);

        // Assert
        Assert.Equal(ResolutionDecision.Review, result.Decision);
        Assert.False(result.WasAutoMerged);
        Assert.Contains("Manual review recommended", result.Explanation);
        Assert.Contains("Identity requires manual review before final resolution", result.Warnings);
    }

    [Fact]
    public async Task ResolveIdentityAsync_WithConflict_ShouldRequireReview()
    {
        // Arrange
        var identity = CreateTestIdentity("John", "Doe");
        var normalizedIdentity = CreateTestIdentity("John", "Doe");
        var candidateIdentity = CreateTestIdentity("John", "Doe");

        var conflictMatch = new IdentityMatch
        {
            SourceIdentity = normalizedIdentity,
            CandidateIdentity = candidateIdentity,
            OverallScore = -1.0 // Negative score indicates conflict
        };

        var matchResult = new MatchResult
        {
            SourceIdentity = normalizedIdentity,
            Matches = new List<IdentityMatch> { conflictMatch }
        };

        _mockNormalizationService.Setup(x => x.NormalizeIdentity(identity))
            .Returns(normalizedIdentity);
        _mockMatchingService.Setup(x => x.FindMatchesAsync(normalizedIdentity, It.IsAny<MatchingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchResult);
        _mockStorageService.Setup(x => x.StoreIdentityAsync(normalizedIdentity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedIdentity);
        _mockStorageService.Setup(x => x.UpdateIdentityAsync(It.IsAny<Identity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Identity identity, CancellationToken _) => identity);

        // Act
        var result = await _service.ResolveIdentityAsync(identity);

        // Assert
        Assert.Equal(ResolutionDecision.Review, result.Decision);
        Assert.Contains("Deterministic conflict detected", result.Explanation);
    }

    [Fact]
    public void MergeIdentities_ShouldCombineInformationCorrectly()
    {
        // Arrange
        var primaryIdentity = new Identity
        {
            Id = Guid.NewGuid(),
            PersonalInfo = new PersonalInfo
            {
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1990, 1, 1)
            },
            ContactInfo = new ContactInfo
            {
                Email = "john.doe@example.com"
            },
            Identifiers = new List<Identifier>
            {
                new Identifier { Type = "SSN", Value = "123456789" }
            }
        };

        var secondaryIdentity = new Identity
        {
            Id = Guid.NewGuid(),
            PersonalInfo = new PersonalInfo
            {
                FirstName = "John",
                MiddleName = "William", // Additional info
                LastName = "Doe"
            },
            ContactInfo = new ContactInfo
            {
                Phone = "555-123-4567" // Additional info
            },
            Identifiers = new List<Identifier>
            {
                new Identifier { Type = "DL", Value = "D1234567" } // Additional identifier
            }
        };

        // Act
        var merged = _service.MergeIdentities(primaryIdentity, secondaryIdentity);

        // Assert
        Assert.Equal(primaryIdentity.Id, merged.Id);
        Assert.Equal("John", merged.PersonalInfo.FirstName);
        Assert.Equal("William", merged.PersonalInfo.MiddleName); // From secondary
        Assert.Equal("Doe", merged.PersonalInfo.LastName);
        Assert.Equal(new DateTime(1990, 1, 1), merged.PersonalInfo.DateOfBirth);
        Assert.Equal("john.doe@example.com", merged.ContactInfo.Email);
        Assert.Equal("555-123-4567", merged.ContactInfo.Phone); // From secondary
        Assert.Equal(2, merged.Identifiers.Count); // Combined identifiers
    }

    [Fact]
    public async Task ResolveIdentityAsync_ShouldIncludeAuditData()
    {
        // Arrange
        var identity = CreateTestIdentity("John", "Doe");
        var normalizedIdentity = CreateTestIdentity("John", "Doe");
        var matchResult = new MatchResult
        {
            SourceIdentity = normalizedIdentity,
            Matches = new List<IdentityMatch>(),
            Algorithm = "Test Algorithm",
            CandidatesEvaluated = 5
        };

        _mockNormalizationService.Setup(x => x.NormalizeIdentity(identity))
            .Returns(normalizedIdentity);
        _mockMatchingService.Setup(x => x.FindMatchesAsync(normalizedIdentity, It.IsAny<MatchingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchResult);
        _mockStorageService.Setup(x => x.StoreIdentityAsync(normalizedIdentity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedIdentity);

        // Act
        var result = await _service.ResolveIdentityAsync(identity);

        // Assert
        Assert.NotNull(result.AuditData);
        Assert.Equal("Test Algorithm", result.AuditData["algorithm"]);
        Assert.Equal(5, result.AuditData["candidatesEvaluated"]);
        Assert.Equal(0, result.AuditData["matchCount"]);
        Assert.True(result.AuditData.ContainsKey("thresholds"));
        Assert.True(result.AuditData.ContainsKey("processingTimeMs"));
    }

    private Identity CreateTestIdentity(string firstName, string lastName)
    {
        return new Identity
        {
            Id = Guid.NewGuid(),
            PersonalInfo = new PersonalInfo
            {
                FirstName = firstName,
                LastName = lastName
            },
            ContactInfo = new ContactInfo(),
            Identifiers = new List<Identifier>()
        };
    }
}
