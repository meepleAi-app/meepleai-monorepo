# Backend Testing Patterns

**Reference Guide** for MeepleAI backend test implementation patterns.

---

## Quick Reference

| Pattern | Use Case | Example File |
|---------|----------|--------------|
| Unit Handler Test | Test command/query handlers in isolation | `GetUnreadCountQueryHandlerTests.cs` |
| Domain Entity Test | Test domain behavior and invariants | `NotificationTests.cs` |
| Validator Test | Test FluentValidation rules | `GuidValidatorTests.cs` |
| Integration Test | Test DB persistence with Testcontainers | `GameRepositoryIntegrationTests.cs` |
| Endpoint Test | Test HTTP routing layer | `AuthenticationEndpointsIntegrationTests.cs` |

---

## Unit Test Patterns

### Handler Tests (Command/Query)

**Pattern**: Mock all dependencies, test handler logic in isolation.

```csharp
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "YourContext")]
public class YourHandlerTests
{
    private readonly Mock<IRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly YourHandler _handler;

    public YourHandlerTests()
    {
        _mockRepository = new Mock<IRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new YourHandler(_mockRepository.Object, _mockUnitOfWork.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        var act = () => new YourHandler(null!, _mockUnitOfWork.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repository");
    }

    #endregion

    #region Null Guard Tests

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    #endregion

    #region Success Tests

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsExpectedResult()
    {
        // Arrange
        var command = new YourCommand(/* parameters */);
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Entity());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Error Tests

    [Fact]
    public async Task Handle_WhenEntityNotFound_ThrowsDomainException()
    {
        // Arrange
        var command = new YourCommand(Guid.NewGuid());
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity?)null);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<DomainException>(act);
        exception.Message.Should().Contain("not found");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        // Arrange
        var command = new YourCommand();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), token)).ReturnsAsync(new Entity());

        // Act
        await _handler.Handle(command, token);

        // Assert
        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), token), Times.Once);
    }

    #endregion
}
```

### Domain Entity Tests

**Pattern**: Test entity behavior, invariants, and state transitions.

```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "YourContext")]
public class EntityTests
{
    #region Creation Tests

    [Fact]
    public void Create_WithValidParameters_CreatesEntity()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Test Name";

        // Act
        var entity = new Entity(id, name);

        // Assert
        entity.Id.Should().Be(id);
        entity.Name.Should().Be(name);
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithEmptyId_ThrowsArgumentException()
    {
        var act = () => new Entity(Guid.Empty, "Name");
        act.Should().Throw<ArgumentException>().WithMessage("*Id*");
    }

    #endregion

    #region Behavior Tests

    [Fact]
    public void MarkAsComplete_SetsCompletedAtTimestamp()
    {
        // Arrange
        var entity = CreateTestEntity();

        // Act
        entity.MarkAsComplete();

        // Assert
        entity.CompletedAt.Should().NotBeNull();
        entity.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void MarkAsComplete_WhenAlreadyComplete_ThrowsDomainException()
    {
        // Arrange
        var entity = CreateTestEntity();
        entity.MarkAsComplete();

        // Act
        var act = () => entity.MarkAsComplete();

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*already complete*");
    }

    #endregion

    #region Helper Methods

    private static Entity CreateTestEntity() => new(Guid.NewGuid(), "Test Entity");

    #endregion
}
```

### Validator Tests

**Pattern**: Test FluentValidation rules with various input scenarios.

```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "YourContext")]
public class YourValidatorTests
{
    private readonly YourValidator _validator = new();

    #region Valid Input Tests

    [Fact]
    public void Validate_WithValidCommand_ReturnsNoErrors()
    {
        // Arrange
        var command = new YourCommand(
            UserId: Guid.NewGuid(),
            Name: "Valid Name",
            Email: "test@example.com"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region Required Field Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithInvalidName_ReturnsValidationError(string? name)
    {
        // Arrange
        var command = new YourCommand(UserId: Guid.NewGuid(), Name: name!, Email: "test@example.com");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WithEmptyUserId_ReturnsValidationError()
    {
        var command = new YourCommand(UserId: Guid.Empty, Name: "Name", Email: "test@example.com");
        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    #endregion

    #region Format Validation Tests

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("missing@domain")]
    [InlineData("@nodomain.com")]
    public void Validate_WithInvalidEmail_ReturnsValidationError(string email)
    {
        var command = new YourCommand(UserId: Guid.NewGuid(), Name: "Name", Email: email);
        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    #endregion

    #region Length Validation Tests

    [Fact]
    public void Validate_WithNameExceedingMaxLength_ReturnsValidationError()
    {
        var longName = new string('a', 201); // Exceeds 200 char limit
        var command = new YourCommand(UserId: Guid.NewGuid(), Name: longName, Email: "test@example.com");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage.Contains("200"));
    }

    #endregion
}
```

---

## Integration Test Patterns

### Repository Integration Tests

**Pattern**: Use Testcontainers for real DB testing with isolation.

```csharp
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "YourContext")]
public class YourRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext _dbContext = null!;
    private YourRepository _repository = null!;

    public YourRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_yourrepo_{Guid.NewGuid():N}";
        var connString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connString)
            .Options;

        var mediator = TestDbContextFactory.CreateMockMediator();
        var eventCollector = TestDbContextFactory.CreateMockEventCollector();
        _dbContext = new MeepleAiDbContext(options, mediator.Object, eventCollector.Object);

        await _dbContext.Database.MigrateAsync();
        _repository = new YourRepository(_dbContext, eventCollector.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task AddAsync_ValidEntity_PersistsToDatabase()
    {
        // Arrange
        var entity = CreateTestEntity();

        // Act
        await _repository.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear(); // Clear tracking for fresh read

        // Assert
        var retrieved = await _repository.GetByIdAsync(entity.Id, TestContext.Current.CancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(entity.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    private static Entity CreateTestEntity() => new(Guid.NewGuid(), $"Test_{Guid.NewGuid():N}");
}
```

### Endpoint Integration Tests

**Pattern**: Test HTTP routing layer with WebApplicationFactory.

```csharp
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "YourContext")]
public class YourEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _databaseName = string.Empty;

    public YourEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_endpoints_{Guid.NewGuid():N}";
        var connString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _factory = new TestWebApplicationFactory(connString);
        _client = _factory.CreateClient();

        // Run migrations
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task Get_ReturnsOkWithData()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/your-endpoint");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<YourResponse>();
        content.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_WithValidData_ReturnsCreated()
    {
        // Arrange
        var request = new CreateRequest { Name = "Test" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/your-endpoint", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateRequest { Name = "" }; // Invalid

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/your-endpoint", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

---

## Test Organization

### File Structure

```
tests/Api.Tests/
├── BoundedContexts/
│   └── {ContextName}/
│       ├── Application/
│       │   ├── Handlers/
│       │   │   ├── {Handler}Tests.cs           # Unit tests
│       │   │   └── Integration/
│       │   │       └── {Handler}IntegrationTests.cs
│       │   └── Validators/
│       │       └── {Validator}Tests.cs
│       ├── Domain/
│       │   ├── {Entity}Tests.cs
│       │   └── ValueObjects/
│       │       └── {ValueObject}Tests.cs
│       └── Infrastructure/
│           └── {Repository}IntegrationTests.cs
├── Integration/
│   └── {Context}/
│       └── {Endpoint}IntegrationTests.cs
├── Infrastructure/
│   ├── SharedTestcontainersFixture.cs
│   └── TestWebApplicationFactory.cs
└── Constants/
    └── TestCategories.cs
```

### Test Traits

Always apply traits for filtering and organization:

```csharp
[Trait("Category", TestCategories.Unit)]          // or Integration
[Trait("BoundedContext", "GameManagement")]       // Bounded context name
public class YourTests { }
```

### Run Commands

```bash
# All unit tests
dotnet test --filter "Category=Unit"

# All integration tests
dotnet test --filter "Category=Integration"

# Specific bounded context
dotnet test --filter "BoundedContext=GameManagement"

# Specific test class
dotnet test --filter "FullyQualifiedName~YourHandlerTests"
```

---

## Common Patterns

### Testing Domain Events

```csharp
[Fact]
public async Task Handle_RaisesDomainEvent()
{
    // Arrange
    var mockEventCollector = new Mock<IDomainEventCollector>();
    var entity = new Entity(mockEventCollector.Object);

    // Act
    entity.DoSomething();

    // Assert
    mockEventCollector.Verify(
        e => e.Collect(It.Is<SomethingHappenedEvent>(ev => ev.EntityId == entity.Id)),
        Times.Once);
}
```

### Testing Soft Delete

```csharp
[Fact]
public async Task Delete_SetsIsDeletedFlag()
{
    // Arrange
    var entity = CreateTestEntity();
    await _repository.AddAsync(entity, CancellationToken.None);
    await _dbContext.SaveChangesAsync();

    // Act
    entity.Delete();
    await _dbContext.SaveChangesAsync();
    _dbContext.ChangeTracker.Clear();

    // Assert - Entity should not be found due to query filter
    var result = await _repository.GetByIdAsync(entity.Id, CancellationToken.None);
    result.Should().BeNull();

    // Verify it exists in DB with IsDeleted=true
    var rawEntity = await _dbContext.Set<Entity>()
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(e => e.Id == entity.Id);
    rawEntity.Should().NotBeNull();
    rawEntity!.IsDeleted.Should().BeTrue();
}
```

### Testing Concurrency

```csharp
[Fact]
public async Task Update_WithStaleVersion_ThrowsConcurrencyException()
{
    // Arrange
    var entity = CreateTestEntity();
    await _repository.AddAsync(entity, CancellationToken.None);
    await _dbContext.SaveChangesAsync();

    // Simulate concurrent update
    using var scope2 = _factory.Services.CreateScope();
    var dbContext2 = scope2.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var entity2 = await dbContext2.Set<Entity>().FindAsync(entity.Id);
    entity2!.UpdateName("Concurrent Update");
    await dbContext2.SaveChangesAsync();

    // Act - Try to update with stale version
    entity.UpdateName("Original Update");
    var act = () => _dbContext.SaveChangesAsync();

    // Assert
    await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
}
```

---

## References

- [Testing README](../README.md)
- [Testcontainers Best Practices](./testcontainers-best-practices.md)
- [Integration Test Optimization](./INTEGRATION_TEST_OPTIMIZATION.md)

---

**Last Updated**: 2026-01-27
**Maintainer**: Backend Team
