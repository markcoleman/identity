# Identity Resolution

A hobby application for simple identity resolution using C# .NET 8.

## Overview

This application provides a basic identity resolution system that can match and consolidate identity records from various sources. It's designed as a learning project to explore identity matching algorithms and patterns.

## Features

- **Identity Matching**: Compare and match identity records based on various attributes
- **Data Normalization**: Clean and standardize input data for better matching
- **Confidence Scoring**: Assign confidence levels to matches
- **RESTful API**: HTTP API for identity operations
- **In-Memory Storage**: Quick setup with in-memory data store (can be extended to use SQL Server)

## Project Structure

```
├── src/
│   ├── IdentityResolution.Core/     # Core business logic and domain models
│   └── IdentityResolution.Api/      # Web API project
├── tests/
│   └── IdentityResolution.Tests/    # Unit and integration tests
├── docs/                            # Documentation
└── .devcontainer/                   # Development container configuration
```

## Getting Started

### Prerequisites

- Docker (for devcontainer)
- VS Code with Remote-Containers extension

### Running with DevContainer

1. Clone this repository
2. Open in VS Code
3. When prompted, choose "Reopen in Container"
4. Wait for the container to build and initialize
5. Run the application:
   ```bash
   dotnet run --project src/IdentityResolution.Api
   ```

### Running Locally

If you have .NET 8 SDK installed locally:

```bash
# Restore packages
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test

# Run the API
dotnet run --project src/IdentityResolution.Api
```

## API Endpoints

Once running, the API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `http://localhost:5000/swagger`

### Key Endpoints

- `POST /api/identities` - Add a new identity
- `GET /api/identities/{id}` - Get an identity by ID
- `POST /api/identities/match` - Find potential matches for an identity
- `POST /api/identities/resolve` - Resolve and merge identities

## Core Concepts

### Identity Record
An identity record contains:
- Basic information (name, email, phone)
- Demographic data (date of birth, address)
- Identifiers (SSN, driver's license, etc.)

### Matching Algorithm
The system uses configurable matching rules based on:
- Exact matches on unique identifiers
- Fuzzy matching on names using string similarity
- Weighted scoring across multiple attributes
- Configurable confidence thresholds

### Resolution Process
1. **Normalization**: Clean and standardize input data
2. **Blocking**: Group records for efficient comparison
3. **Matching**: Apply similarity algorithms
4. **Scoring**: Calculate confidence scores
5. **Resolution**: Determine final matches based on thresholds

## Configuration

Key configuration options in `appsettings.json`:

```json
{
  "IdentityResolution": {
    "MatchingThreshold": 0.8,
    "AutoMergeThreshold": 0.95,
    "EnableFuzzyMatching": true
  }
}
```

## Development

### Adding New Matching Rules

1. Implement `IMatchingRule` interface in the Core project
2. Register the rule in dependency injection
3. Configure rule weights and parameters

### Testing

Run all tests:
```bash
dotnet test --logger "console;verbosity=detailed"
```

Run with coverage:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Contributing

This is a hobby project, but contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Roadmap

- [ ] Advanced matching algorithms (machine learning)
- [ ] Database persistence options
- [ ] Batch processing capabilities
- [ ] Web UI for identity management
- [ ] Export/import functionality
- [ ] Audit logging and history tracking