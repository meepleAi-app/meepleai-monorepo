# MeepleAI API Tests

Unit and integration tests for the MeepleAI ASP.NET Core backend.

## Tech Stack

- **Framework**: xUnit
- **Mocking**: Moq
- **Integration Testing**: Testcontainers (PostgreSQL, Qdrant)
- **Web Testing**: Microsoft.AspNetCore.Mvc.Testing
- **Coverage**: Coverlet

## Running Tests

```bash
# From apps/api directory

# Run all tests
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportsDirectory=coverage

# Run specific test class
dotnet test --filter "FullyQualifiedName~GameServiceTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~Should_ReturnGame_When_GameExists"

# Run with verbose output
dotnet test --verbosity normal
```

## Test Structure

```
Api.Tests/
  Services/              # Service layer unit tests
    GameServiceTests.cs
    AuthServiceTests.cs
  Integration/           # Integration tests with real dependencies
    GameEndpointsTests.cs
  Helpers/              # Test utilities and fixtures
```

## Test Naming Convention

All test methods must follow BDD-style naming:

```csharp
Should_[ExpectedBehavior]_When_[Condition]
```

### Examples

```csharp
public class GameServiceTests
{
    [Fact]
    public async Task Should_ReturnGame_When_GameExists()
    {
        // Arrange
        var gameId = 1;
        var mockRepo = new Mock<IGameRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(gameId))
            .ReturnsAsync(new GameEntity { Id = gameId, Name = "Chess" });

        var service = new GameService(mockRepo.Object);

        // Act
        var result = await service.GetGameAsync(gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Chess", result.Name);
    }

    [Fact]
    public async Task Should_ThrowNotFoundException_When_GameNotFound()
    {
        // Arrange
        var gameId = 999;
        var mockRepo = new Mock<IGameRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(gameId))
            .ReturnsAsync((GameEntity?)null);

        var service = new GameService(mockRepo.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetGameAsync(gameId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Should_ThrowValidationException_When_GameNameIsInvalid(
        string invalidName)
    {
        // Arrange
        var mockRepo = new Mock<IGameRepository>();
        var service = new GameService(mockRepo.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateGameAsync(new CreateGameRequest { Name = invalidName }));
    }
}
```

### Integration Tests

Integration tests follow the same naming pattern:

```csharp
public class GameEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GameEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Should_ReturnGamesList_When_GamesExist()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/games");

        // Assert
        response.EnsureSuccessStatusCode();
        var games = await response.Content.ReadFromJsonAsync<List<GameDto>>();
        Assert.NotNull(games);
    }

    [Fact]
    public async Task Should_Return401_When_UserNotAuthenticated()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/stats");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

For complete testing guidelines including frontend patterns, common scenarios, and anti-patterns, see:
- **[Testing Guidelines (README.test.md)](../../../README.test.md)** - Comprehensive guide for both frontend and backend testing

## Test Organization

### Arrange-Act-Assert Pattern

All tests should follow the AAA pattern:

```csharp
[Fact]
public async Task Should_CreateGame_When_ValidDataProvided()
{
    // Arrange - Set up test data and dependencies
    var mockRepo = new Mock<IGameRepository>();
    var service = new GameService(mockRepo.Object);
    var request = new CreateGameRequest { Name = "Catan" };

    // Act - Execute the operation being tested
    var result = await service.CreateGameAsync(request);

    // Assert - Verify the outcome
    Assert.NotNull(result);
    Assert.Equal("Catan", result.Name);
    mockRepo.Verify(r => r.AddAsync(It.IsAny<GameEntity>()), Times.Once);
}
```

### Using Testcontainers

For tests requiring real database or vector store:

```csharp
public class QdrantServiceIntegrationTests : IAsyncLifetime
{
    private QdrantContainer _qdrantContainer = null!;
    private QdrantService _service = null!;

    public async Task InitializeAsync()
    {
        _qdrantContainer = new QdrantBuilder()
            .WithImage("qdrant/qdrant:v1.7.4")
            .Build();

        await _qdrantContainer.StartAsync();

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(_qdrantContainer.GetConnectionString())
        };

        _service = new QdrantService(httpClient, Mock.Of<ILogger<QdrantService>>());
    }

    public async Task DisposeAsync()
    {
        await _qdrantContainer.DisposeAsync();
    }

    [Fact]
    public async Task Should_CreateCollection_When_CollectionDoesNotExist()
    {
        // Test implementation
    }
}
```

### Common Patterns

| Scenario | Example Test Name |
|----------|------------------|
| **Success Case** | `Should_ReturnGame_When_GameExists` |
| **Not Found** | `Should_Return404_When_GameNotFound` |
| **Validation** | `Should_ThrowValidationException_When_InputIsInvalid` |
| **Authentication** | `Should_Return401_When_UserNotAuthenticated` |
| **Authorization** | `Should_Return403_When_UserLacksPermission` |
| **Database** | `Should_SaveToDatabase_When_EntityIsValid` |
| **External API** | `Should_CallExternalApi_When_DataNeeded` |
| **Caching** | `Should_ReturnCachedData_When_CacheHit` |

## CI/CD Integration

Tests run automatically on every PR and push to `main` via GitHub Actions.

**Environment Variables for CI**:
- `CI=true` - Detected automatically in GitHub Actions
- `DocnetRuntime=linux` - Set for PDF extraction in Linux containers
- Database connection strings configured in workflow

**Services**:
- PostgreSQL 16.4 on port 5432
- Qdrant on port 6333

## Troubleshooting

### Tests fail locally but pass in CI
- Check Docker/Linux-specific issues
- Verify Testcontainers are starting correctly
- Ensure environment variables match CI configuration

### "Collection already exists" in Qdrant tests
- Use `IAsyncLifetime` to ensure cleanup between tests
- Call `DisposeAsync()` to stop containers

### Entity Framework migrations not applied
- Tests use in-memory SQLite by default (not real PostgreSQL)
- Integration tests with Testcontainers use real PostgreSQL with migrations

## Additional Resources

- **[Project Overview (CLAUDE.md)](../../../CLAUDE.md)** - Complete monorepo documentation
- **[Testing Guidelines (README.test.md)](../../../README.test.md)** - Test naming conventions and best practices
- **xUnit Documentation**: https://xunit.net/
- **Moq Documentation**: https://github.com/moq/moq4
- **Testcontainers .NET**: https://dotnet.testcontainers.org/
