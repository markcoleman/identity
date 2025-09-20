using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using IdentityResolution.Core.Services.Implementations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IdentityResolution.Tests.Core.Services;

public class IdentityMatchingServiceTests
{
    private readonly Mock<IIdentityStorageService> _mockStorageService;
    private readonly Mock<ITokenizationService> _mockTokenizationService;
    private readonly IdentityMatchingService _service;

    public IdentityMatchingServiceTests()
    {
        _mockStorageService = new Mock<IIdentityStorageService>();
        _mockTokenizationService = new Mock<ITokenizationService>();
        var mockLogger = new Mock<ILogger<IdentityMatchingService>>();

        _service = new IdentityMatchingService(
            _mockStorageService.Object,
            _mockTokenizationService.Object,
            mockLogger.Object);
    }

    [Fact]
    public void CompareIdentities_WithExactSSNAndDOB_ShouldReturnPerfectMatch()
    {
        // Arrange
        var identity1 = CreateIdentityWithSSNAndDOB("123-45-6789", new DateTime(1990, 1, 1));
        var identity2 = CreateIdentityWithSSNAndDOB("123-45-6789", new DateTime(1990, 1, 1));

        _mockTokenizationService.Setup(x => x.TokenizeSSN("123456789"))
            .Returns("SameToken");

        // Act
        var result = _service.CompareIdentities(identity1, identity2);

        // Assert
        Assert.Equal(1.0, result.OverallScore);
        Assert.True(result.IsAutoMergeCandidate);
        Assert.Contains("Exact SSN + DOB match", result.MatchReasons);
    }

    [Fact]
    public void CompareIdentities_WithSSNMatchButDOBDiffers_ShouldFlagForReview()
    {
        // Arrange
        var identity1 = CreateIdentityWithSSNAndDOB("123-45-6789", new DateTime(1990, 1, 1));
        var identity2 = CreateIdentityWithSSNAndDOB("123-45-6789", new DateTime(1990, 1, 2)); // Different DOB

        _mockTokenizationService.Setup(x => x.TokenizeSSN("123456789"))
            .Returns("SameToken");

        // Act
        var result = _service.CompareIdentities(identity1, identity2);

        // Assert
        Assert.Equal(0.0, result.OverallScore);
        Assert.Equal(MatchStatus.RequiresReview, result.Status);
        Assert.Contains("SSN matches but DOB differs - flagging for review", result.MatchReasons);
    }

    [Fact]
    public void CompareIdentities_WithHighProbabilisticScore_ShouldReturnAutoMerge()
    {
        // Arrange - Include more matching fields to reach auto-merge threshold
        var identity1 = CreateIdentityWithAllFields("John", "Doe", "john.doe@example.com", "555-123-4567", new DateTime(1990, 1, 1));
        var identity2 = CreateIdentityWithAllFields("John", "Doe", "john.doe@example.com", "555-123-4567", new DateTime(1990, 1, 1));

        var config = new MatchingConfiguration
        {
            AutoMergeThreshold = 0.97,
            ReviewThreshold = 0.90
        };

        // Act
        var result = _service.CompareIdentities(identity1, identity2, config);

        // Assert - Should get perfect score since all fields match
        Assert.True(result.OverallScore >= config.AutoMergeThreshold, 
            $"Expected score >= {config.AutoMergeThreshold}, but got {result.OverallScore}");
        Assert.True(result.IsAutoMergeCandidate);
        Assert.Equal(MatchStatus.Pending, result.Status);
    }

    [Fact]
    public void CompareIdentities_WithMediumProbabilisticScore_ShouldRequireReview()
    {
        // Arrange - Use name + email match (0.75 total) which is between thresholds
        var identity1 = CreateIdentityWithNameAndEmail("John", "Doe", "john.doe@example.com");
        var identity2 = CreateIdentityWithNameAndEmail("John", "Doe", "john.doe@example.com");

        var config = new MatchingConfiguration
        {
            AutoMergeThreshold = 0.97,
            ReviewThreshold = 0.70  // Lower threshold to catch this score
        };

        // Act
        var result = _service.CompareIdentities(identity1, identity2, config);

        // Assert - Score should be 0.75 (FirstName 0.2 + LastName 0.25 + Email 0.3)
        Assert.True(result.OverallScore >= config.ReviewThreshold && result.OverallScore < config.AutoMergeThreshold);
        Assert.Equal(MatchStatus.RequiresReview, result.Status);
    }

    [Fact]
    public void CompareIdentities_WithLowProbabilisticScore_ShouldReject()
    {
        // Arrange
        var identity1 = CreateIdentityWithNameAndEmail("John", "Doe", "john.doe@example.com");
        var identity2 = CreateIdentityWithNameAndEmail("Jane", "Smith", "jane.smith@example.com");

        var config = new MatchingConfiguration
        {
            AutoMergeThreshold = 0.97,
            ReviewThreshold = 0.90
        };

        // Act
        var result = _service.CompareIdentities(identity1, identity2, config);

        // Assert
        Assert.True(result.OverallScore < config.ReviewThreshold);
        Assert.Equal(MatchStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task FindMatchesAsync_WithCandidates_ShouldReturnSortedMatches()
    {
        // Arrange
        var sourceIdentity = CreateIdentityWithNameAndEmail("John", "Doe", "john.doe@example.com");
        
        var candidates = new List<Identity>
        {
            CreateIdentityWithNameAndEmail("John", "Doe", "john.doe@example.com"), // High match
            CreateIdentityWithNameAndEmail("Jon", "Doe", "john.d@example.com"),   // Medium match
            CreateIdentityWithNameAndEmail("Jane", "Smith", "jane@example.com")   // Low match
        };

        _mockStorageService.Setup(x => x.GetAllIdentitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);

        var config = new MatchingConfiguration
        {
            MinimumMatchThreshold = 0.6,
            MaxResults = 10
        };

        // Act
        var result = await _service.FindMatchesAsync(sourceIdentity, config);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sourceIdentity, result.SourceIdentity);
        Assert.True(result.Matches.Count > 0);
        
        // Verify matches are sorted by score (descending)
        for (int i = 0; i < result.Matches.Count - 1; i++)
        {
            Assert.True(result.Matches[i].OverallScore >= result.Matches[i + 1].OverallScore);
        }
    }

    private Identity CreateIdentityWithSSNAndDOB(string ssn, DateTime dob)
    {
        return new Identity
        {
            Id = Guid.NewGuid(),
            PersonalInfo = new PersonalInfo
            {
                DateOfBirth = dob
            },
            ContactInfo = new ContactInfo(),
            Identifiers = new List<Identifier>
            {
                new Identifier
                {
                    Type = IdentifierTypes.SocialSecurityNumber,
                    Value = ssn
                }
            }
        };
    }

    private Identity CreateIdentityWithNameAndEmail(string firstName, string lastName, string email)
    {
        return new Identity
        {
            Id = Guid.NewGuid(),
            PersonalInfo = new PersonalInfo
            {
                FirstName = firstName,
                LastName = lastName
            },
            ContactInfo = new ContactInfo
            {
                Email = email
            },
            Identifiers = new List<Identifier>()
        };
    }

    private Identity CreateIdentityWithAllFields(string firstName, string lastName, string email, string phone, DateTime dob)
    {
        return new Identity
        {
            Id = Guid.NewGuid(),
            PersonalInfo = new PersonalInfo
            {
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = dob
            },
            ContactInfo = new ContactInfo
            {
                Email = email,
                Phone = phone
            },
            Identifiers = new List<Identifier>()
        };
    }
}