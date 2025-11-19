# Integration Tests Quick Reference

**Quick cheat sheet for writing performant integration tests in MeepleAI**

Last Updated: 2025-11-16

---

## Quick Start: Write a New Integration Test

### Step 1: Add Collection Attribute

```csharp
[Collection(nameof(DatabaseCollection))] // ✅ Share database across test classes
public class YourRepositoryTests
{
    private readonly DatabaseFixture _fixture;

    public YourRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }
}
```

### Step 2: Reset Database Before Each Test

```csharp
private async Task InitializeTestAsync()
{
    await _fixture.ResetDatabaseAsync(); // ✅ Fast reset (~10-20ms)
    _dbContext = _fixture.CreateDbContext();
    _repository = new YourRepository(_dbContext);
}

[Fact]
public async Task YourTest()
{
    // Arrange
    await InitializeTestAsync(); // ✅ Always call first

    // Act
    // ... your test code

    // Assert
    // ... your assertions
}
```

### Step 3: Use AAA Pattern

```csharp
[Fact]
public async Task AddAsync_WithValidEntity_SavesSuccessfully()
{
    // Arrange
    await InitializeTestAsync();
    var entity = CreateTestEntity();

    // Act
    await _repository.AddAsync(entity);

    // Assert
    var retrieved = await _repository.GetByIdAsync(entity.Id);
    retrieved.Should().NotBeNull();
    retrieved!.Name.Should().Be(entity.Name);
}
```

---

## Common Patterns

### Read-Only Query

```csharp
public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    return await _dbContext.Users
        .AsNoTracking() // ✅ Faster, no tracking conflicts
        .FirstOrDefaultAsync(u => u.Id == id, ct);
}
```

### Update with Tracking Safety

```csharp
public async Task UpdateAsync(User user, CancellationToken ct = default)
{
    var entity = user.ToEntity();

    // Detach existing tracked entity if present
    var tracked = _dbContext.ChangeTracker.Entries<UserEntity>()
        .FirstOrDefault(e => e.Entity.Id == entity.Id);
    if (tracked != null)
        tracked.State = EntityState.Detached;

    _dbContext.Users.Update(entity);
    await _dbContext.SaveChangesAsync(ct);
}
```

### Independent DbContext for Concurrent Tests

```csharp
[Fact]
public async Task ConcurrentOperations_WorkCorrectly()
{
    await InitializeTestAsync();

    // Use independent context for concurrent operations
    await using var independentContext = _fixture.CreateDbContext();
    var independentRepo = new YourRepository(independentContext);

    // ... concurrent test code
}
```

---

## Performance Checklist

### ✅ DO

- ✅ Use `[Collection(nameof(DatabaseCollection))]` to share database
- ✅ Call `await _fixture.ResetDatabaseAsync()` before each test
- ✅ Use `AsNoTracking()` for read-only queries
- ✅ Detach entities in `UpdateAsync()` to prevent tracking conflicts
- ✅ Dispose DbContext with `await using` or `using`
- ✅ Pin specific image versions (e.g., `postgres:16-alpine`)
- ✅ Use random ports with `.WithPortBinding(port, true)`

### ❌ DON'T

- ❌ Create container per test class (use shared fixture instead)
- ❌ Share DbContext across tests (create fresh per test)
- ❌ Hardcode connection strings or ports
- ❌ Use `postgres:latest` (pin specific version)
- ❌ Enable container reuse in CI/CD (only for local dev)
- ❌ Forget to reset database between tests
- ❌ Use TRUNCATE manually (use Respawn instead)

---

## Performance Comparison

| Pattern | Time | Notes |
|---------|------|-------|
| **Container Startup** |  |  |
| Per-class (legacy) | 2.5s per class | ❌ Slow |
| Shared fixture | 2.5s total | ✅ Fast |
| **Database Reset** |  |  |
| Manual TRUNCATE | 50-100ms | ❌ Slow |
| Respawn | 15-30ms | ✅ Fast |
| Respawn.Postgres | 7-15ms | ✅ Fastest |
| **Queries** |  |  |
| With tracking | 100ms | ❌ Slower |
| AsNoTracking | 70ms | ✅ 30% faster |

---

## Test Template

```csharp
using Api.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.YourContext.Infrastructure.Persistence;

[Collection(nameof(DatabaseCollection))]
public class YourRepositoryTests
{
    private readonly DatabaseFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private YourRepository _repository = null!;

    public YourRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task InitializeTestAsync()
    {
        await _fixture.ResetDatabaseAsync();
        _dbContext = _fixture.CreateDbContext();
        _repository = new YourRepository(_dbContext);
    }

    [Fact]
    public async Task AddAsync_WithValidEntity_SavesSuccessfully()
    {
        // Arrange
        await InitializeTestAsync();
        var entity = CreateTestEntity();

        // Act
        await _repository.AddAsync(entity);

        // Assert
        var retrieved = await _repository.GetByIdAsync(entity.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(entity.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonexistentId_ReturnsNull()
    {
        // Arrange
        await InitializeTestAsync();

        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithModifiedEntity_SavesChanges()
    {
        // Arrange
        await InitializeTestAsync();
        var entity = CreateTestEntity();
        await _repository.AddAsync(entity);

        // Modify entity
        entity.Name = "Updated Name";

        // Act
        await _repository.UpdateAsync(entity);

        // Assert
        var retrieved = await _repository.GetByIdAsync(entity.Id);
        retrieved!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteAsync_WithExistingEntity_RemovesFromDatabase()
    {
        // Arrange
        await InitializeTestAsync();
        var entity = CreateTestEntity();
        await _repository.AddAsync(entity);

        // Act
        await _repository.DeleteAsync(entity.Id);

        // Assert
        var retrieved = await _repository.GetByIdAsync(entity.Id);
        retrieved.Should().BeNull();
    }

    private static YourEntity CreateTestEntity()
    {
        return new YourEntity
        {
            Id = Guid.NewGuid(),
            Name = $"Test-{Guid.NewGuid()}",
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

---

## Local Development: Enable Container Reuse

**One-time setup**:

```bash
# Create ~/.testcontainers.properties
echo "testcontainers.reuse.enable=true" > ~/.testcontainers.properties
```

**Update DatabaseFixture**:

```csharp
public DatabaseFixture()
{
    var isLocalDev = Environment.GetEnvironmentVariable("CI") == null;

    var builder = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("meepleai_test");

    if (isLocalDev)
    {
        builder.WithReuse(true); // ✅ 50x faster for local iterations
    }

    _container = builder.Build();
}
```

**Performance**: First run ~10s, subsequent runs ~200ms (50x faster)

---

## Troubleshooting

### Problem: "Cannot track entity because another instance is already tracked"

**Solution**: Apply detach pattern in `UpdateAsync()`:

```csharp
var tracked = _dbContext.ChangeTracker.Entries<YourEntity>()
    .FirstOrDefault(e => e.Entity.Id == entity.Id);
if (tracked != null)
    tracked.State = EntityState.Detached;
```

### Problem: Tests fail with "port already in use"

**Solution**: Use random ports:

```csharp
.WithPortBinding(5432, true) // ✅ Random host port
```

### Problem: Slow database reset between tests

**Solution**: Use Respawn instead of manual TRUNCATE:

```bash
dotnet add package Respawn.Postgres --version 6.2.1
```

### Problem: Container not reused in local development

**Solution**: Check `~/.testcontainers.properties`:

```bash
cat ~/.testcontainers.properties
# Should contain: testcontainers.reuse.enable=true
```

---

## Run Tests

```bash
# All tests
cd apps/api
dotnet test

# Integration tests only
dotnet test --filter "Category=Integration"

# Specific test class
dotnet test --filter "FullyQualifiedName~YourRepositoryTests"

# Verbose output
dotnet test --logger "console;verbosity=detailed"

# With coverage
dotnet test /p:CollectCoverage=true
```

---

## Resources

- **Full Guide**: `docs/02-development/testing/integration-tests-performance-guide.md`
- **Testing Guide**: `docs/02-development/testing/testing-guide.md`
- **Test Architecture**: `apps/api/tests/Api.Tests/TEST_ARCHITECTURE.md`
- **Known Issues**: `docs/07-project-management/tracking/integration-tests-known-issues.md`

---

**Need Help?**
- Check existing tests in `apps/api/tests/Api.Tests/BoundedContexts/`
- Review DatabaseFixture implementation
- Ask in team's testing channel
