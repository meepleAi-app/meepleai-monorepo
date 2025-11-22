# MeepleAI API Tests

Unit and integration tests for the MeepleAI ASP.NET Core backend (DDD/CQRS architecture).

**Architecture**: Domain-Driven Design with 7 Bounded Contexts, CQRS/MediatR pattern
**Coverage**: 90%+ enforced
**Test Count**: 162 tests

## Tech Stack

- **Framework**: xUnit
- **Mocking**: Moq
- **Integration Testing**: Testcontainers (PostgreSQL, Qdrant, Unstructured, SmolDocling)
- **Web Testing**: Microsoft.AspNetCore.Mvc.Testing
- **Coverage**: Coverlet

## Running Tests

```bash
# From apps/api directory

# Run all tests
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportsDirectory=coverage

# Run specific test class (CQRS handlers, not services)
dotnet test --filter "FullyQualifiedName~CreateGameCommandHandlerTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~Should_CreateGame_When_ValidDataProvided"

# Run with verbose output
dotnet test --verbosity normal
```

## Test Structure

Tests are organized by Bounded Context following DDD/CQRS architecture:

```
Api.Tests/
  BoundedContexts/
    Authentication/         # Auth command/query handler tests
    GameManagement/         # Game command/query handler tests
    KnowledgeBase/          # RAG command/query handler tests
    DocumentProcessing/     # PDF processing handler tests
    WorkflowIntegration/    # n8n workflow handler tests
    SystemConfiguration/    # Config handler tests
    Administration/         # Admin handler tests
  Integration/              # Integration tests with real dependencies
  Helpers/                  # Test utilities and fixtures
```

**Note**: Legacy service tests have been replaced with CQRS handler tests as part of the DDD migration (99% complete).

## Test Naming Convention

All test methods must follow BDD-style naming:

```csharp
Should_[ExpectedBehavior]_When_[Condition]
```

### Examples

**CQRS Command Handler Tests** (DDD Architecture):

```csharp
public class CreateGameCommandHandlerTests
{
    [Fact]
    public async Task Should_CreateGame_When_ValidDataProvided()
    {
        // Arrange
        var mockRepo = new Mock<IGameRepository>();
        var handler = new CreateGameCommandHandler(mockRepo.Object);
        var command = new CreateGameCommand
        {
            Name = "Catan",
            MinPlayers = 3,
            MaxPlayers = 4
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Catan", result.Name);
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Game>()), Times.Once);
    }

    [Fact]
    public async Task Should_ThrowValidationException_When_GameNameIsEmpty()
    {
        // Arrange
        var mockRepo = new Mock<IGameRepository>();
        var handler = new CreateGameCommandHandler(mockRepo.Object);
        var command = new CreateGameCommand { Name = "" };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
```

**CQRS Query Handler Tests**:

```csharp
public class GetGameByIdQueryHandlerTests
{
    [Fact]
    public async Task Should_ReturnGame_When_GameExists()
    {
        // Arrange
        var gameId = 1;
        var mockRepo = new Mock<IGameRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(gameId))
            .ReturnsAsync(new Game { Id = gameId, Name = "Chess" });

        var handler = new GetGameByIdQueryHandler(mockRepo.Object);
        var query = new GetGameByIdQuery { Id = gameId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

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
            .ReturnsAsync((Game?)null);

        var handler = new GetGameByIdQueryHandler(mockRepo.Object);
        var query = new GetGameByIdQuery { Id = gameId };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(query, CancellationToken.None));
    }
}
```

### Integration Tests

Integration tests verify entire CQRS flow (HTTP → MediatR → Handler → Repository):

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

        // Act - Endpoint sends GetAllGamesQuery via IMediator
        var response = await client.GetAsync("/api/v1/games");

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

        // Act - Endpoint sends GetAdminStatsQuery via IMediator
        var response = await client.GetAsync("/api/v1/admin/stats");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Should_CreateGame_When_ValidDataProvided()
    {
        // Arrange
        var client = _factory.CreateClient();
        var command = new { name = "Catan", minPlayers = 3, maxPlayers = 4 };

        // Act - Endpoint sends CreateGameCommand via IMediator
        var response = await client.PostAsJsonAsync("/api/v1/games", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var game = await response.Content.ReadFromJsonAsync<GameDto>();
        Assert.NotNull(game);
        Assert.Equal("Catan", game.Name);
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
    var handler = new CreateGameCommandHandler(mockRepo.Object);
    var command = new CreateGameCommand { Name = "Catan", MinPlayers = 3, MaxPlayers = 4 };

    // Act - Execute the command handler
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert - Verify the outcome
    Assert.NotNull(result);
    Assert.Equal("Catan", result.Name);
    mockRepo.Verify(r => r.AddAsync(It.IsAny<Game>()), Times.Once);
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
| **Command Success** | `Should_CreateGame_When_ValidDataProvided` |
| **Query Success** | `Should_ReturnGame_When_GameExists` |
| **Not Found** | `Should_ThrowNotFoundException_When_GameNotFound` |
| **Validation** | `Should_ThrowValidationException_When_InputIsInvalid` |
| **Authentication** | `Should_Return401_When_UserNotAuthenticated` |
| **Authorization** | `Should_Return403_When_UserLacksPermission` |
| **Domain Logic** | `Should_ApplyBusinessRule_When_ConditionMet` |
| **Repository** | `Should_PersistAggregate_When_CommandHandled` |
| **Integration** | `Should_SendCommand_When_EndpointCalled` |
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

## DDD/CQRS Architecture

**Pattern**: All business logic now follows DDD/CQRS pattern via MediatR:
- **Commands**: Mutate state (Create, Update, Delete)
- **Queries**: Read data (Get, List, Search)
- **Handlers**: Process commands/queries (IRequestHandler<TRequest, TResponse>)
- **Domain**: Pure business logic (Aggregates, Value Objects, Domain Services)
- **Infrastructure**: Data access (Repositories, EF Core)

**Example Flow**:
```
HTTP Request → Endpoint → IMediator.Send(command/query)
  → Handler.Handle() → Domain Logic → Repository → Database
```

**Legacy Services Removed** (2,070 lines):
- ~~GameService~~ → CreateGameCommand, GetGameByIdQuery, etc.
- ~~AuthService~~ → LoginCommand, RegisterCommand, etc.
- ~~UserManagementService~~ → CreateUserCommand, etc.
- ~~PDF Services~~ → UploadPdfCommand, ExtractTextCommand, etc.

**Retained** (Infrastructure/Orchestration):
- RagService (orchestrates hybrid RAG)
- ConfigurationService (3-tier fallback config)
- AdminStatsService (aggregates metrics)
- AlertingService (sends alerts)

## Additional Resources

- **[CLAUDE.md](../../../CLAUDE.md)** - Complete development guide (DDD 99% complete)
- **[DDD Quick Reference](../../../docs/01-architecture/ddd/quick-reference.md)** - DDD patterns and bounded contexts
- **[Architecture Overview](../../../docs/01-architecture/overview/system-architecture.md)** - System architecture (60+ pages)
- **[Testing Guidelines](../../../docs/02-development/testing/test-writing-guide.md)** - Test naming conventions and best practices
- **xUnit Documentation**: https://xunit.net/
- **Moq Documentation**: https://github.com/moq/moq4
- **Testcontainers .NET**: https://dotnet.testcontainers.org/
- **MediatR Documentation**: https://github.com/jbogard/MediatR
