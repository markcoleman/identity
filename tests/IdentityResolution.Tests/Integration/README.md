# Integration Tests

This directory contains integration tests that use testcontainers to spin up ephemeral PostgreSQL, Redis, and Elasticsearch environments for testing the Identity Resolution Service.

## Requirements

- Docker Desktop or Docker Engine running
- At least 2GB available RAM
- Network access to pull Docker images

## Running Integration Tests

### Locally

```bash
# Run all integration tests
dotnet test --filter "Category=Integration"

# Run specific test categories
dotnet test --filter "FullyQualifiedName~DeterministicMatchingIntegrationTests"
dotnet test --filter "FullyQualifiedName~ProbabilisticMatchingIntegrationTests"
dotnet test --filter "FullyQualifiedName~BasicContainerIntegrationTests"
```

### In CI/CD

Integration tests are excluded from regular CI runs due to resource requirements. To run them in GitHub Actions:

1. Include `[run-integration]` in your commit message
2. Push to the `main` branch
3. The integration tests will run as a separate job

## Test Categories

### BasicContainerIntegrationTests
- Database connectivity and basic CRUD operations
- Service initialization and container lifecycle
- Data normalization and tokenization

### DeterministicMatchingIntegrationTests  
- SSN + DOB exact matching scenarios
- High-confidence auto-merge validation
- Conflict detection and review queuing

### ProbabilisticMatchingIntegrationTests
- Threshold-based matching (≥0.97 → AUTO, 0.90–0.97 → REVIEW)
- Fuzzy string matching and name variations
- Email/phone normalization and matching

## Performance

- Container startup: ~15 seconds (PostgreSQL: 3s, Redis: 1s, Elasticsearch: 15s)
- Database operations: 1-2ms per query
- End-to-end matching: ~200-300ms including database lookup
- Automatic cleanup after test completion

## Architecture

The integration tests use a `IntegrationTestBase` class that:

1. Starts PostgreSQL, Redis, and Elasticsearch containers
2. Creates database schema with proper indexes
3. Seeds test data for realistic matching scenarios
4. Configures all services with database-backed implementations
5. Cleans up containers after tests complete

Each test class overrides the seed data to avoid conflicts between test scenarios.