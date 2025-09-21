using IdentityResolution.Core.Models;
using IdentityResolution.Core.Services;
using IdentityResolution.Core.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.Elasticsearch;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace IdentityResolution.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests using testcontainers
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;
    private readonly ElasticsearchContainer _elasticsearchContainer;

    protected ServiceProvider ServiceProvider { get; private set; } = null!;
    protected IdentityDbContext DbContext { get; private set; } = null!;
    protected IIdentityStorageService StorageService { get; private set; } = null!;
    protected IIdentityMatchingService MatchingService { get; private set; } = null!;
    protected IIdentityResolutionService ResolutionService { get; private set; } = null!;
    protected IDataNormalizationService NormalizationService { get; private set; } = null!;
    protected ITokenizationService TokenizationService { get; private set; } = null!;

    protected IntegrationTestBase()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("identity_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithCleanUp(true)
            .Build();

        _elasticsearchContainer = new ElasticsearchBuilder()
            .WithImage("docker.elastic.co/elasticsearch/elasticsearch:7.17.10")
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start containers
        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _redisContainer.StartAsync(),
            _elasticsearchContainer.StartAsync()
        );

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);

        ServiceProvider = services.BuildServiceProvider();

        // Initialize database
        DbContext = ServiceProvider.GetRequiredService<IdentityDbContext>();
        await DbContext.Database.EnsureCreatedAsync();

        // Get services
        StorageService = ServiceProvider.GetRequiredService<IIdentityStorageService>();
        MatchingService = ServiceProvider.GetRequiredService<IIdentityMatchingService>();
        ResolutionService = ServiceProvider.GetRequiredService<IIdentityResolutionService>();
        NormalizationService = ServiceProvider.GetRequiredService<IDataNormalizationService>();
        TokenizationService = ServiceProvider.GetRequiredService<ITokenizationService>();

        // Seed test data
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        ServiceProvider?.Dispose();
        await Task.WhenAll(
            _postgresContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask(),
            _elasticsearchContainer.DisposeAsync().AsTask()
        );
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Database
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(_postgresContainer.GetConnectionString()));

        // Logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Core services
        services.AddScoped<IIdentityStorageService, PostgreSqlIdentityStorageService>();
        services.AddScoped<IIdentityMatchingService, IdentityMatchingService>();
        services.AddScoped<IIdentityResolutionService, IdentityResolutionService>();
        services.AddScoped<IDataNormalizationService, DataNormalizationService>();
        services.AddScoped<ITokenizationService, TokenizationService>();

        // Additional services (optional/nullable for tests)
        services.AddScoped<IAuditService, InMemoryAuditService>();
        services.AddScoped<IReviewQueueService, InMemoryReviewQueueService>();
    }

    protected virtual async Task SeedTestDataAsync()
    {
        // Base implementation seeds sample identities for testing matching logic
        var sampleIdentities = CreateSampleIdentities();

        foreach (var identity in sampleIdentities)
        {
            await StorageService.StoreIdentityAsync(identity);
        }
    }

    protected virtual List<Identity> CreateSampleIdentities()
    {
        return new List<Identity>
        {
            // Deterministic matching test data
            new Identity
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PersonalInfo = new PersonalInfo
                {
                    FirstName = "John",
                    LastName = "Smith",
                    DateOfBirth = DateTime.SpecifyKind(new DateTime(1985, 3, 15), DateTimeKind.Utc)
                },
                ContactInfo = new ContactInfo
                {
                    Email = "john.smith@example.com",
                    Phone = "(555) 123-4567"
                },
                Identifiers = new List<Identifier>
                {
                    new Identifier
                    {
                        Type = IdentifierTypes.SocialSecurityNumber,
                        Value = "123-45-6789"
                    }
                },
                Source = "TestSystem",
                Confidence = 1.0
            },

            // High confidence probabilistic match
            new Identity
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PersonalInfo = new PersonalInfo
                {
                    FirstName = "Jane",
                    LastName = "Johnson",
                    DateOfBirth = DateTime.SpecifyKind(new DateTime(1990, 7, 22), DateTimeKind.Utc)
                },
                ContactInfo = new ContactInfo
                {
                    Email = "jane.johnson@example.com",
                    Phone = "(555) 987-6543"
                },
                Identifiers = new List<Identifier>(),
                Source = "TestSystem",
                Confidence = 0.98
            },

            // Medium confidence probabilistic match
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
    }

    /// <summary>
    /// Create a test identity for matching scenarios
    /// </summary>
    protected Identity CreateTestIdentity(string firstName, string lastName, DateTime? dob = null, string? email = null, string? phone = null, string? ssn = null)
    {
        var identity = new Identity
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PersonalInfo = new PersonalInfo
            {
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = dob.HasValue ? DateTime.SpecifyKind(dob.Value, DateTimeKind.Utc) : null
            },
            ContactInfo = new ContactInfo
            {
                Email = email,
                Phone = phone
            },
            Identifiers = new List<Identifier>()
        };

        if (!string.IsNullOrEmpty(ssn))
        {
            identity.Identifiers.Add(new Identifier
            {
                Type = IdentifierTypes.SocialSecurityNumber,
                Value = ssn
            });
        }

        return identity;
    }
}