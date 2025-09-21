using System.Text;
using System.Text.Json;
using FluentAssertions;
using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using IdentityResolution.Core.Services.Implementations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IdentityResolution.Tests.Core.Services;

public class BatchProcessingServiceTests
{
    private readonly Mock<IIdentityResolutionService> _mockResolutionService;
    private readonly Mock<IDataNormalizationService> _mockNormalizationService;
    private readonly Mock<ILogger<InMemoryBatchProcessingService>> _mockLogger;
    private readonly InMemoryBatchProcessingService _batchProcessingService;

    public BatchProcessingServiceTests()
    {
        _mockResolutionService = new Mock<IIdentityResolutionService>();
        _mockNormalizationService = new Mock<IDataNormalizationService>();
        _mockLogger = new Mock<ILogger<InMemoryBatchProcessingService>>();

        _batchProcessingService = new InMemoryBatchProcessingService(
            _mockResolutionService.Object,
            _mockNormalizationService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessBatchAsync_WithValidJsonData_ShouldProcessSuccessfully()
    {
        // Arrange
        var identities = new List<Identity>
        {
            new Identity
            {
                PersonalInfo = { FirstName = "John", LastName = "Doe", DateOfBirth = DateTime.Parse("1990-01-01") },
                ContactInfo = { Email = "john.doe@example.com" }
            },
            new Identity
            {
                PersonalInfo = { FirstName = "Jane", LastName = "Smith", DateOfBirth = DateTime.Parse("1985-05-15") },
                ContactInfo = { Email = "jane.smith@example.com" }
            }
        };

        var jsonData = JsonSerializer.Serialize(identities);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));

        // Setup mocks
        _mockNormalizationService.Setup(x => x.NormalizeIdentity(It.IsAny<Identity>()))
            .Returns<Identity>(identity => identity);

        _mockResolutionService.Setup(x => x.ResolveIdentityAsync(It.IsAny<Identity>(), It.IsAny<MatchingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolutionResult
            {
                EPID = "EPID-12345",
                Decision = ResolutionDecision.New,
                ResolvedIdentity = new Identity(),
                Matches = new List<IdentityMatch>()
            });

        // Act
        var result = await _batchProcessingService.ProcessBatchAsync(stream, BatchInputFormat.Json);

        // Assert
        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(2);
        result.SuccessfullyProcessed.Should().Be(2);
        result.Failed.Should().Be(0);
        result.Results.Should().HaveCount(2);
        result.DecisionCounts[ResolutionDecision.New].Should().Be(2);
        result.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessBatchAsync_WithValidCsvData_ShouldProcessSuccessfully()
    {
        // Arrange
        var csvData = @"FirstName,LastName,DateOfBirth,Email
John,Doe,1990-01-01,john.doe@example.com
Jane,Smith,1985-05-15,jane.smith@example.com";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvData));

        // Setup mocks
        _mockNormalizationService.Setup(x => x.NormalizeIdentity(It.IsAny<Identity>()))
            .Returns<Identity>(identity => identity);

        _mockResolutionService.Setup(x => x.ResolveIdentityAsync(It.IsAny<Identity>(), It.IsAny<MatchingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolutionResult
            {
                EPID = "EPID-12345",
                Decision = ResolutionDecision.Auto,
                ResolvedIdentity = new Identity(),
                Matches = new List<IdentityMatch>
                {
                    new IdentityMatch { OverallScore = 0.95 }
                }
            });

        // Act
        var result = await _batchProcessingService.ProcessBatchAsync(stream, BatchInputFormat.Csv);

        // Assert
        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(2);
        result.SuccessfullyProcessed.Should().Be(2);
        result.Failed.Should().Be(0);
        result.Results.Should().HaveCount(2);
        result.DecisionCounts[ResolutionDecision.Auto].Should().Be(2);

        // Verify the parsed data
        result.Results[0].InputIdentity.PersonalInfo.FirstName.Should().Be("John");
        result.Results[0].InputIdentity.PersonalInfo.LastName.Should().Be("Doe");
        result.Results[0].InputIdentity.ContactInfo.Email.Should().Be("john.doe@example.com");
    }

    [Fact]
    public async Task ProcessBatchAsync_WithMalformedData_ShouldHandleErrors()
    {
        // Arrange
        var identities = new List<Identity>
        {
            new Identity
            {
                PersonalInfo = { FirstName = "John", LastName = "Doe" },
                ContactInfo = { Email = "john.doe@example.com" }
            },
            new Identity
            {
                // Missing required fields to trigger validation error
                ContactInfo = { Email = "invalid-email" }
            }
        };

        var jsonData = JsonSerializer.Serialize(identities);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));

        // Setup mocks
        _mockNormalizationService.Setup(x => x.NormalizeIdentity(It.IsAny<Identity>()))
            .Returns<Identity>(identity => identity);

        _mockResolutionService.Setup(x => x.ResolveIdentityAsync(It.IsAny<Identity>(), It.IsAny<MatchingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolutionResult
            {
                EPID = "EPID-12345",
                Decision = ResolutionDecision.New,
                ResolvedIdentity = new Identity(),
                Matches = new List<IdentityMatch>()
            });

        // Act
        var result = await _batchProcessingService.ProcessBatchAsync(stream, BatchInputFormat.Json);

        // Assert
        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(2);
        result.SuccessfullyProcessed.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Results.Should().HaveCount(2);

        // Check that the failed record has error information
        var failedRecord = result.Results.FirstOrDefault(r => !r.IsSuccess);
        failedRecord.Should().NotBeNull();
        failedRecord!.ErrorCode.Should().Be(BatchErrorCodes.MissingRequiredField);
        failedRecord.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessBatchAsync_WithParallelProcessing_ShouldRespectMaxParallelism()
    {
        // Arrange
        var identities = Enumerable.Range(1, 10).Select(i => new Identity
        {
            PersonalInfo = { FirstName = $"User{i}", LastName = "Test" },
            ContactInfo = { Email = $"user{i}@example.com" }
        }).ToList();

        var jsonData = JsonSerializer.Serialize(identities);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));

        var configuration = new BatchProcessingConfiguration
        {
            MaxParallelism = 3,
            BatchSize = 2
        };

        // Setup mocks with delay to test parallelism
        _mockNormalizationService.Setup(x => x.NormalizeIdentity(It.IsAny<Identity>()))
            .Returns<Identity>(identity => identity);

        _mockResolutionService.Setup(x => x.ResolveIdentityAsync(It.IsAny<Identity>(), It.IsAny<MatchingConfiguration>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(10); // Small delay to test parallelism
                return new ResolutionResult
                {
                    EPID = $"EPID-{Guid.NewGuid():N}",
                    Decision = ResolutionDecision.New,
                    ResolvedIdentity = new Identity(),
                    Matches = new List<IdentityMatch>()
                };
            });

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _batchProcessingService.ProcessBatchAsync(stream, BatchInputFormat.Json, configuration);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(10);
        result.SuccessfullyProcessed.Should().Be(10);
        result.Failed.Should().Be(0);

        // Should process faster than sequential (10 * 10ms = 100ms)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    [Fact]
    public async Task ScheduleBatchProcessingAsync_ShouldReturnJobId()
    {
        // Arrange
        var jobRequest = new BatchProcessingJobRequest
        {
            Source = "/tmp/test.json",
            InputFormat = BatchInputFormat.Json,
            RequestedBy = "test-user"
        };

        // Act
        var jobId = await _batchProcessingService.ScheduleBatchProcessingAsync(jobRequest);

        // Assert
        jobId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetBatchJobStatusAsync_WithValidJobId_ShouldReturnStatus()
    {
        // Arrange
        var jobRequest = new BatchProcessingJobRequest
        {
            Source = "/tmp/test.json",
            InputFormat = BatchInputFormat.Json,
            RequestedBy = "test-user"
        };

        var jobId = await _batchProcessingService.ScheduleBatchProcessingAsync(jobRequest);

        // Act
        var status = await _batchProcessingService.GetBatchJobStatusAsync(jobId);

        // Assert
        status.Should().NotBeNull();
        status.JobId.Should().Be(jobId);
        status.Status.Should().BeOneOf(JobStatus.Queued, JobStatus.Running, JobStatus.Completed, JobStatus.Failed);
    }

    [Fact]
    public async Task GetBatchJobStatusAsync_WithInvalidJobId_ShouldReturnFailedStatus()
    {
        // Arrange
        var invalidJobId = Guid.NewGuid();

        // Act
        var status = await _batchProcessingService.GetBatchJobStatusAsync(invalidJobId);

        // Assert
        status.Should().NotBeNull();
        status.JobId.Should().Be(invalidJobId);
        status.Status.Should().Be(JobStatus.Failed);
        status.ErrorMessage.Should().Be("Job not found");
    }

    [Fact]
    public async Task ProcessBatchAsync_WithConfiguration_ShouldRespectLimits()
    {
        // Arrange
        var identities = new List<Identity>
        {
            new Identity
            {
                PersonalInfo = { FirstName = "John", LastName = "Doe" },
                ContactInfo = { Email = "john.doe@example.com" }
            }
        };

        var jsonData = JsonSerializer.Serialize(identities);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));

        var configuration = new BatchProcessingConfiguration
        {
            MaxParallelism = 1,
            BatchSize = 1,
            ContinueOnError = false,
            Timeout = TimeSpan.FromMinutes(1)
        };

        // Setup mocks
        _mockNormalizationService.Setup(x => x.NormalizeIdentity(It.IsAny<Identity>()))
            .Returns<Identity>(identity => identity);

        _mockResolutionService.Setup(x => x.ResolveIdentityAsync(It.IsAny<Identity>(), It.IsAny<MatchingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolutionResult
            {
                EPID = "EPID-12345",
                Decision = ResolutionDecision.New,
                ResolvedIdentity = new Identity(),
                Matches = new List<IdentityMatch>()
            });

        // Act
        var result = await _batchProcessingService.ProcessBatchAsync(stream, BatchInputFormat.Json, configuration);

        // Assert
        result.Should().NotBeNull();
        result.Configuration.Should().BeEquivalentTo(configuration);
        result.TotalProcessingTime.Should().BeLessThan(configuration.Timeout);
    }

    [Theory]
    [InlineData("first_name,last_name,email\nJohn,Doe,john@example.com")]
    [InlineData("FirstName,LastName,Email\nJane,Smith,jane@example.com")]
    public async Task ProcessBatchAsync_WithDifferentCsvHeaders_ShouldParseCorrectly(string csvData)
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvData));

        // Setup mocks
        _mockNormalizationService.Setup(x => x.NormalizeIdentity(It.IsAny<Identity>()))
            .Returns<Identity>(identity => identity);

        _mockResolutionService.Setup(x => x.ResolveIdentityAsync(It.IsAny<Identity>(), It.IsAny<MatchingConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolutionResult
            {
                EPID = "EPID-12345",
                Decision = ResolutionDecision.New,
                ResolvedIdentity = new Identity(),
                Matches = new List<IdentityMatch>()
            });

        // Act
        var result = await _batchProcessingService.ProcessBatchAsync(stream, BatchInputFormat.Csv);

        // Assert
        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(1);
        result.SuccessfullyProcessed.Should().Be(1);

        var processedRecord = result.Results.First();
        processedRecord.InputIdentity.PersonalInfo.FirstName.Should().NotBeNullOrEmpty();
        processedRecord.InputIdentity.PersonalInfo.LastName.Should().NotBeNullOrEmpty();
        processedRecord.InputIdentity.ContactInfo.Email.Should().NotBeNullOrEmpty();
    }
}
