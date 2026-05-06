# Backend Testing Patterns

**Reference guide** for MeepleAI backend test implementation.

---

## Pattern Index

| Pattern | Use Case | File | Traits |
|---------|----------|------|--------|
| **Handler** | Command/Query handler isolation | `*HandlerTests.cs` | `Unit`, `{BoundedContext}` |
| **Entity** | Domain behavior + invariants | `*EntityTests.cs` | `Unit`, `{BoundedContext}` |
| **Validator** | FluentValidation rules | `*ValidatorTests.cs` | `Unit`, `{BoundedContext}` |
| **Repository** | DB persistence | `*RepositoryIntegrationTests.cs` | `Integration`, `{BoundedContext}` |
| **Endpoint** | HTTP routing layer | `*EndpointsIntegrationTests.cs` | `Integration`, `{BoundedContext}` |

---

## Unit Test Templates

### Handler Tests (CQRS)

**Pattern**: Mock dependencies, test handler logic

```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "YourContext")]
public class YourHandlerTests
{
    private readonly Mock<IRepository> _mockRepository;
    private readonly YourHandler _handler;

    // Constructor Tests: Null guard
    [Fact] public void Constructor_WithNullRepository_ThrowsArgumentNullException() { ... }

    // Null Guard Tests: Null command
    [Fact] public async Task Handle_WithNullCommand_ThrowsArgumentNullException() { ... }

    // Success Tests: Valid command
    [Fact]
    public async Task Handle_WithValidCommand_ReturnsExpectedResult()
    {
        // Arrange: Setup mocks
        _mockRepository.Setup(r => r.GetByIdAsync(...)).ReturnsAsync(new Entity());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(r => r.GetByIdAsync(...), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(...), Times.Once);
    }

    // Error Tests: Not found
    [Fact] public async Task Handle_WhenEntityNotFound_ThrowsDomainException() { ... }

    // Cancellation Tests: Token propagation
    [Fact] public async Task Handle_PassesCancellationTokenToRepository() { ... }
}
```

### Entity Tests (Domain)

```csharp
[Trait("Category", TestCategories.Unit)]
public class EntityTests
{
    // Creation: Valid params, empty ID guard
    [Fact] public void Create_WithValidParameters_CreatesEntity() { ... }
    [Fact] public void Create_WithEmptyId_ThrowsArgumentException() { ... }

    // Behavior: State transitions
    [Fact] public void MarkAsComplete_SetsCompletedAtTimestamp() { ... }
    [Fact] public void MarkAsComplete_WhenAlreadyComplete_ThrowsDomainException() { ... }

    // Helper
    private static Entity CreateTestEntity() => new(Guid.NewGuid(), "Test");
}
```

### Validator Tests (FluentValidation)

```csharp
[Trait("Category", TestCategories.Unit)]
public class YourValidatorTests
{
    private readonly YourValidator _validator = new();

    // Valid Input
    [Fact] public void Validate_WithValidCommand_ReturnsNoErrors() { ... }

    // Required Fields
    [Theory]
    [InlineData(null), InlineData(""), InlineData("   ")]
    public void Validate_WithInvalidName_ReturnsValidationError(string? name) { ... }

    // Format Validation
    [Theory]
    [InlineData("invalid-email"), InlineData("@nodomain.com")]
    public void Validate_WithInvalidEmail_ReturnsValidationError(string email) { ... }

    // Length Validation
    [Fact] public void Validate_WithNameExceedingMaxLength_ReturnsValidationError() { ... }
}
```

---

## Integration Test Templates

### Repository Tests (Testcontainers)

```csharp
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "YourContext")]
public class YourRepositoryIntegrationTests : IAsyncLifetime
{
    private string _databaseName = string.Empty;
    private MeepleAiDbContext _dbContext = null!;
    private YourRepository _repository = null!;

    // Initialize: Create isolated DB, run migrations
    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_{Guid.NewGuid():N}";
        var connString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = new MeepleAiDbContext(options, mediator, eventCollector);
        await _dbContext.Database.MigrateAsync();
        _repository = new YourRepository(_dbContext, eventCollector);
    }

    // Dispose: Cleanup DB + context
    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // Test: Add + retrieve
    [Fact]
    public async Task AddAsync_ValidEntity_PersistsToDatabase()
    {
        await _repository.AddAsync(entity, ct);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();  // Fresh read
        var retrieved = await _repository.GetByIdAsync(entity.Id, ct);
        retrieved.Should().NotBeNull();
    }
}
```

### Endpoint Tests (WebApplicationFactory)

```csharp
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
public class YourEndpointsIntegrationTests : IAsyncLifetime
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var connString = await _fixture.CreateIsolatedDatabaseAsync($"test_{Guid.NewGuid():N}");
        _factory = new TestWebApplicationFactory(connString);
        _client = _factory.CreateClient();
        // Run migrations
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    [Fact] public async Task Get_ReturnsOkWithData() { ... }
    [Fact] public async Task Post_WithValidData_ReturnsCreated() { ... }
    [Fact] public async Task Post_WithInvalidData_ReturnsBadRequest() { ... }
}
```

---

## Common Patterns

| Pattern | Code Snippet |
|---------|--------------|
| **Domain Events** | `mockEventCollector.Verify(e => e.Collect(It.Is<Event>(ev => ev.EntityId == id)), Times.Once)` |
| **Soft Delete** | `entity.Delete(); _dbContext.SaveChanges(); _dbContext.ChangeTracker.Clear(); result = await _repository.GetByIdAsync(id); result.Should().BeNull();` |
| **Concurrency** | Simulate concurrent update → `SaveChangesAsync()` → `Should().ThrowAsync<DbUpdateConcurrencyException>()` |

---

## File Organization

```
tests/Api.Tests/
  BoundedContexts/{Context}/
    Application/Handlers/*Tests.cs, Validators/*Tests.cs
    Domain/*EntityTests.cs, ValueObjects/*Tests.cs
    Infrastructure/*RepositoryIntegrationTests.cs
  Integration/{Context}/*EndpointsIntegrationTests.cs
  Infrastructure/SharedTestcontainersFixture.cs, TestWebApplicationFactory.cs
```

**Traits**: `[Trait("Category", "Unit|Integration")]`, `[Trait("BoundedContext", "{Name}")]`

**Run Commands**:
```bash
dotnet test --filter "Category=Unit"  # Unit only
dotnet test --filter "BoundedContext=GameManagement"  # By context
dotnet test --filter "FullyQualifiedName~YourHandlerTests"  # Specific class
```

---

**Updated**: 2026-01-27 • **Maintainer**: Backend Team
