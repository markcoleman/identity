# Identity Resolution - GitHub Copilot Instructions

**ALWAYS follow these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the information provided here.**

Identity Resolution is a C# .NET 8 hobby application that provides identity matching and consolidation services through a RESTful API. The system can match identity records based on configurable algorithms and confidence thresholds.

## Working Effectively

### Prerequisites & Setup
- **Required**: .NET 8 SDK (version 8.0.119 or later)
- **Optional**: Docker and VS Code with Remote-Containers extension for devcontainer support
- **Development Environment**: Can run natively on Linux, Windows, or macOS, or via devcontainer

### Bootstrap, Build, and Test Commands

**NEVER CANCEL builds or long-running commands - Wait for completion!**

1. **Restore packages** (takes ~30 seconds):
   ```bash
   dotnet restore
   ```
   - **Timeout**: Set to 60+ seconds minimum
   - **NEVER CANCEL**: Package restore may take up to 30 seconds on first run

2. **Build the solution** (takes ~15 seconds):
   ```bash
   dotnet build
   ```
   - **Timeout**: Set to 60+ seconds minimum
   - **Expected**: Build warnings about missing XML documentation are normal and can be ignored
   - **NEVER CANCEL**: Build typically completes in 10-15 seconds but may take longer on slower systems

3. **Run all tests** (takes ~5 seconds):
   ```bash
   dotnet test --logger "console;verbosity=detailed"
   ```
   - **Timeout**: Set to 30+ seconds minimum
   - **Expected Result**: All 13 tests should pass
   - **NEVER CANCEL**: Test execution is fast but allow adequate timeout

4. **Run tests with coverage**:
   ```bash
   dotnet test --collect:"XPlat Code Coverage"
   ```

### Code Formatting and Quality

**ALWAYS run formatting before committing changes:**

```bash
dotnet format
```

**Verify code formatting is clean:**
```bash
dotnet format --verify-no-changes
```
- **Required**: Must return exit code 0 (no formatting issues)
- **Critical**: Code formatting violations will prevent clean builds

## Running the Application

### Start the API Server
```bash
dotnet run --project src/IdentityResolution.Api
```

- **Expected Output**: 
  ```
  info: Microsoft.Hosting.Lifetime[14]
        Now listening on: http://localhost:5000
  info: Microsoft.Hosting.Lifetime[0]
        Application started. Press Ctrl+C to shut down.
  ```
- **Endpoints Available**:
  - HTTP: `http://localhost:5000`
  - HTTPS: `https://localhost:5001` 
  - Swagger UI: `http://localhost:5000/swagger`
- **Startup Time**: ~5-10 seconds

### API Endpoints and Testing

**Key REST Endpoints:**
- `GET /api/identities` - Get all identities
- `POST /api/identities` - Add a new identity  
- `GET /api/identities/{id}` - Get identity by ID
- `POST /api/identities/match` - Find potential matches for an identity
- `POST /api/identities/resolve` - Resolve and merge identities

## Validation and Testing

### Manual Validation Scenarios

**ALWAYS test these scenarios after making changes:**

1. **Basic API Health Check:**
   ```bash
   curl -s "http://localhost:5000/api/identities"
   ```
   - **Expected**: Returns `[]` (empty array) on fresh instance

2. **Create Identity Test:**
   ```bash
   curl -X POST "http://localhost:5000/api/identities" \
     -H "Content-Type: application/json" \
     -d '{
       "personalInfo": {
         "firstName": "John",
         "lastName": "Doe", 
         "dateOfBirth": "1990-01-01"
       },
       "contactInfo": {
         "email": "john.doe@example.com",
         "phone": "555-123-4567"
       }
     }'
   ```
   - **Expected**: Returns identity object with generated ID and normalized data
   - **Verify**: Phone number is formatted as "(555) 123-4567"
   - **Verify**: Email is normalized to lowercase

3. **Retrieve Created Identity:**
   ```bash
   curl "http://localhost:5000/api/identities"
   ```
   - **Expected**: Returns array with the created identity

### Build and Test Validation
**ALWAYS run before committing changes:**
```bash
dotnet build && dotnet test && dotnet format --verify-no-changes
```

## Project Structure and Navigation

```
├── src/
│   ├── IdentityResolution.Core/     # Core business logic and domain models
│   │   ├── Models/                  # Identity, MatchResult, configuration classes
│   │   └── Services/                # Business logic interfaces and implementations
│   └── IdentityResolution.Api/      # Web API project
│       ├── Controllers/             # REST API endpoints
│       ├── Program.cs              # Application startup and DI configuration
│       └── appsettings.json        # Configuration (thresholds, matching rules)
├── tests/
│   └── IdentityResolution.Tests/    # Unit and integration tests
│       └── Core/Services/          # Service layer tests
├── docs/                            # Architecture and API documentation
└── .devcontainer/                   # Development container configuration
```

### Key Files to Know

- **`src/IdentityResolution.Core/Models/Identity.cs`** - Core identity data model
- **`src/IdentityResolution.Core/Services/IIdentityServices.cs`** - Service interfaces
- **`src/IdentityResolution.Api/Controllers/IdentitiesController.cs`** - REST API implementation
- **`src/IdentityResolution.Api/appsettings.json`** - Matching configuration and thresholds
- **`tests/IdentityResolution.Tests/Core/Services/`** - Comprehensive test suite

### Adding New Features

When implementing new matching rules or services:

1. **Always implement the interface first** in `src/IdentityResolution.Core/Services/`
2. **Register new services** in `src/IdentityResolution.Api/Program.cs` DI container
3. **Add comprehensive tests** in `tests/IdentityResolution.Tests/Core/Services/`
4. **Update configuration** in `appsettings.json` if needed

## Configuration

**Key configuration in `src/IdentityResolution.Api/appsettings.json`:**
```json
{
  "IdentityResolution": {
    "MatchingThreshold": 0.6,      // Minimum score for potential matches
    "AutoMergeThreshold": 0.9,     // Score for automatic merging  
    "EnableFuzzyMatching": true,   // Enable string similarity matching
    "MaxResults": 10               // Maximum matches to return
  }
}
```

## Common Gotchas and Troubleshooting

### Build Issues
- **XML Documentation Warnings**: Normal and can be ignored (56 warnings expected)
- **Formatting Errors**: Run `dotnet format` to fix automatically
- **Missing .NET SDK**: Ensure .NET 8.0.119+ is installed

### Runtime Issues  
- **Port Already in Use**: Kill existing `dotnet run` processes or use different ports
- **Empty API Responses**: Expected behavior - in-memory storage starts empty
- **CORS Issues**: CORS is configured to allow all origins in development

### Testing Issues
- **Tests Failing**: Ensure you run `dotnet build` before `dotnet test`
- **Timeout Issues**: Always set adequate timeouts (60s+ for builds, 30s+ for tests)

## DevContainer Support

**For VS Code + Docker users:**
1. Open repository in VS Code
2. Choose "Reopen in Container" when prompted  
3. Wait for container to build (~2-3 minutes first time)
4. Run: `dotnet run --project src/IdentityResolution.Api`

**Container includes:**
- .NET 8 SDK
- Useful .NET tools (dotnet-ef, dotnet-aspnet-codegenerator, etc.)
- VS Code extensions for C# development
- Port forwarding for 5000/5001

## Performance and Timing Expectations

- **Package Restore**: 20-30 seconds (first time)
- **Build**: 10-15 seconds  
- **Test Suite**: 3-5 seconds (13 tests)
- **API Startup**: 5-10 seconds
- **Formatting Check**: 1-2 seconds

**CRITICAL**: Always set timeouts to 2-3x these values to avoid premature cancellation.