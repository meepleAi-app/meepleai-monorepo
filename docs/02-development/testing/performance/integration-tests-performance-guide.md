# Integration Tests Performance Optimization Guide

**Last Updated**: 2025-11-16
**Status**: Production Ready
**Audience**: Backend Developers, QA Engineers

---

## Table of Contents

1. [Overview](#overview)
2. [Current Performance Issues](#current-performance-issues)
3. [Industry Best Practices](#industry-best-practices)
4. [Recommended Optimizations](#recommended-optimizations)
5. [Implementation Patterns](#implementation-patterns)
6. [Performance Benchmarks](#performance-benchmarks)
7. [Migration Guide](#migration-guide)
8. [Resources](#resources)

---

## Overview

This guide provides comprehensive strategies for optimizing integration test performance in the MeepleAI backend. Integration tests are essential for validating database operations, cross-context workflows, and domain events, but they can become slow if not properly optimized.

**Key Metrics**:
- **Current**: ~2-3 seconds per test class (container startup)
- **Target**: <500ms per test class (with optimizations)
- **Best Case**: ~10-50ms per test (after first run with shared fixtures)

---

## Current Performance Issues

### 1. Container Creation Per Test Class

**Problem**: Each test class creates a new PostgreSQL container using `IntegrationTestBase<T>`.

```csharp
public async ValueTask InitializeAsync()
{
    _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase(DatabaseName)
        .Build();

    await _postgresContainer.StartAsync(); // ~2-3 seconds
}
```

**Impact**:
- 15 test classes × 2.5s = **37.5 seconds** just for container startup
- No reuse across test classes
- Unnecessary Docker overhead

### 2. Manual Database Reset

**Current Implementation** (IntegrationTestBase.cs:137-187):

```csharp
protected async Task ResetDatabaseAsync()
{
    // Get all table names
    var tableNames = await tempContext.Database
        .SqlQueryRaw<string>(@"SELECT tablename FROM pg_tables...")
        .ToListAsync();

    // Disable FK constraints
    await tempContext.Database.ExecuteSqlRawAsync(
        "SET session_replication_role = 'replica';");

    // Truncate each table
    foreach (var tableName in tableNames)
    {
        await tempContext.Database.ExecuteSqlRawAsync(
            $"TRUNCATE TABLE \"{tableName}\" CASCADE;");
    }

    // Re-enable FK constraints
    await tempContext.Database.ExecuteSqlRawAsync(
        "SET session_replication_role = 'origin';");
}
```

**Issues**:
- Multiple database round-trips
- Manual FK constraint management
- No caching of table dependency order
- ~50-100ms overhead per test

### 3. EF Core Tracking Conflicts

**Problem**: Tests fail with `Cannot track entity because another instance is already tracked` (See: `docs/07-project-management/tracking/integration-tests-known-issues.md`).

**Root Cause**:
```csharp
// Repository.UpdateAsync() tracks entities globally
var entity = session.ToEntity();
_dbContext.GameSessions.Update(entity); // ❌ Conflict if already tracked
```

**Impact**: 5/15 cross-context tests fail (33%)

### 4. No Parallel Execution

**Current**: Tests run sequentially due to:
- Unique database per test class
- No collection-based resource sharing
- DbContext instances not thread-safe

### 5. No Container Reuse (Local Development)

**Current**: Containers are destroyed after each test run, even during local development.

**Impact**: Developers waste time waiting for container startup on every test run.

---

## Industry Best Practices

Based on research from Microsoft, Testcontainers team, and .NET testing experts (2024-2025):

### 1. **Shared Fixtures with xUnit Collections**

✅ **Recommended**: Use `ICollectionFixture<T>` to share containers across test classes

**Benefits**:
- 1 container for N test classes
- Faster execution (amortized startup cost)
- Efficient resource usage

**Trade-offs**:
- Requires disciplined state cleanup
- Limits xUnit parallelization (tests in same collection run sequentially)

### 2. **Respawn Library for Database Reset**

✅ **Highly Recommended**: Use [Respawn](https://github.com/jbogard/Respawn) for intelligent database cleanup

**Performance**:
- **3-13x faster** than manual TRUNCATE
- **13x faster** with Respawn.Postgres (optimized for PostgreSQL)
- Caches table dependency graph after first reset

**How It Works**:
1. Analyzes SQL metadata to build FK dependency graph
2. Deletes data in deterministic order (no FK constraint issues)
3. Caches deletion plan for subsequent resets
4. ~10-20ms per reset (vs. 50-100ms manual)

### 3. **AsNoTracking for Read Operations**

✅ **Best Practice**: Use `AsNoTracking()` for queries that don't need change tracking

```csharp
public async Task<T?> GetByIdAsync(Guid id)
{
    return await _dbContext.Set<TEntity>()
        .AsNoTracking() // ✅ Don't track read-only queries
        .FirstOrDefaultAsync(e => e.Id == id);
}
```

**Benefits**:
- Prevents tracking conflicts
- ~30% faster queries (per EF Core benchmarks)
- Reduces memory usage

### 4. **Container Reuse (Local Development Only)**

✅ **Recommended for Local Dev**: Enable container reuse with `.WithReuse(true)`

**Setup**:
```bash
# ~/.testcontainers.properties
testcontainers.reuse.enable=true
```

```csharp
_postgresContainer = new PostgreSqlBuilder()
    .WithReuse(true) // ✅ Reuse across test runs
    .Build();
```

**Performance**:
- First run: ~10s (container startup)
- Subsequent runs: ~200ms (connection only)
- **50x improvement** for iterative development

**⚠️ Important**: Disable in CI/CD (security risk, shared state)

### 5. **Image Pinning**

✅ **Required**: Pin specific image versions

```csharp
.WithImage("postgres:16-alpine") // ✅ Specific version
.WithImage("postgres:latest")     // ❌ Unpredictable updates
```

### 6. **Dynamic Port Binding**

✅ **Required**: Use random ports to avoid conflicts

```csharp
.WithPortBinding(5432, true) // ✅ Random host port
.WithPortBinding(5432, 5432)  // ❌ Hardcoded port
```

---

## Recommended Optimizations

### Priority 1: Shared Database Fixture (HIGH IMPACT)

**Estimated Time**: 2-3 hours
**Performance Gain**: 50-70% faster test suite
**Complexity**: Medium

**Implementation**:
1. Create `DatabaseFixture` class with `IAsyncLifetime`
2. Define `[CollectionDefinition]` for test groups
3. Migrate test classes to use `[Collection]` attribute
4. Add Respawn for database cleanup

### Priority 2: Respawn Integration (HIGH IMPACT)

**Estimated Time**: 1-2 hours
**Performance Gain**: 3-13x faster database reset
**Complexity**: Low

**Implementation**:
1. Add NuGet package: `Respawn.Postgres`
2. Replace `ResetDatabaseAsync()` with Respawn
3. Configure checkpoint with table exclusions

### Priority 3: AsNoTracking Pattern (MEDIUM IMPACT)

**Estimated Time**: 2-4 hours
**Performance Gain**: 30% faster queries, fixes tracking conflicts
**Complexity**: Low

**Implementation**:
1. Update repository `GetByIdAsync()` methods
2. Add `AsNoTracking()` to read-only queries
3. Audit `UpdateAsync()` for tracking conflicts

### Priority 4: Container Reuse (LOCAL DEV ONLY)

**Estimated Time**: 30 minutes
**Performance Gain**: 50x for local development
**Complexity**: Low

**Implementation**:
1. Add `.WithReuse(true)` to container builders
2. Document setup in testing guide
3. Ensure disabled in CI/CD

### Priority 5: Parallel Execution Support (LONG-TERM)

**Estimated Time**: 4-8 hours
**Performance Gain**: 2-4x with parallel runners
**Complexity**: High

**Implementation**:
1. Separate test collections by resource isolation
2. Use independent DbContext per test
3. Configure xUnit max parallel threads

---

## Implementation Patterns

### Pattern 1: xUnit Collection Fixture with Testcontainers

**Step 1: Create Database Fixture**

```csharp
// apps/api/tests/Api.Tests/Infrastructure/DatabaseFixture.cs
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private Respawner _respawner = null!;

    public string ConnectionString { get; private set; } = null!;

    public DatabaseFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("meepleai_test")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithPortBinding(5432, true) // Random port
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        // Start container once for all tests in collection
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Apply migrations
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        // Initialize Respawn checkpoint
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            TablesToIgnore = new[] { new Table("__EFMigrationsHistory") }
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Resets database to clean state using Respawn (3-13x faster than TRUNCATE).
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public MeepleAiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(ConnectionString)
            .EnableSensitiveDataLogging()
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        return new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
    }
}
```

**Step 2: Define Collection**

```csharp
// apps/api/tests/Api.Tests/Infrastructure/DatabaseCollection.cs
[CollectionDefinition(nameof(DatabaseCollection))]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    // This class has no code, it's just a marker for xUnit
    // All tests in this collection will share the same DatabaseFixture instance
}
```

**Step 3: Use in Test Classes**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/Authentication/Infrastructure/Persistence/UserRepositoryTests.cs
[Collection(nameof(DatabaseCollection))] // ✅ Share database across test classes
public class UserRepositoryTests
{
    private readonly DatabaseFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private UserRepository _repository = null!;

    public UserRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // Reset database before each test for isolation
    private async Task InitializeTestAsync()
    {
        await _fixture.ResetDatabaseAsync(); // ✅ Fast Respawn reset (~10-20ms)
        _dbContext = _fixture.CreateDbContext();
        _repository = new UserRepository(_dbContext);
    }

    [Fact]
    public async Task AddAsync_WithValidUser_SavesSuccessfully()
    {
        // Arrange
        await InitializeTestAsync();
        var user = CreateTestUser();

        // Act
        await _repository.AddAsync(user);

        // Assert
        var retrieved = await _repository.GetByIdAsync(user.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Email.Should().Be(user.Email);
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
}
```

### Pattern 2: Respawn Integration

**Installation**:

```bash
cd apps/api/tests/Api.Tests
dotnet add package Respawn.Postgres --version 6.2.1
```

**Usage**:

```csharp
using Npgsql;
using Respawn;
using Respawn.Graph;

// One-time initialization
private static Respawner _respawner;

public static async Task InitializeRespawnerAsync(string connectionString)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
    {
        DbAdapter = DbAdapter.Postgres,
        SchemasToInclude = new[] { "public" },
        TablesToIgnore = new[]
        {
            new Table("__EFMigrationsHistory") // Don't delete migrations table
        },
        WithReseed = true // Reset auto-increment sequences
    });
}

// Fast reset between tests
public static async Task ResetDatabaseAsync(string connectionString)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await _respawner.ResetAsync(connection); // ✅ ~10-20ms
}
```

**Performance Comparison**:

| Method | Time | Notes |
|--------|------|-------|
| Manual TRUNCATE (current) | 50-100ms | Multiple round-trips, manual FK handling |
| Respawn (standard) | 15-30ms | Cached deletion plan, deterministic order |
| Respawn.Postgres (optimized) | 7-15ms | PostgreSQL-specific optimizations |

### Pattern 3: AsNoTracking Repository Pattern

**Before (Slow, Tracking Conflicts)**:

```csharp
public async Task<GameSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    var entity = await _dbContext.GameSessions
        .Include(gs => gs.User)
        .Include(gs => gs.Game)
        .FirstOrDefaultAsync(gs => gs.Id == id, ct);

    return entity?.ToDomain(); // ❌ Entity is tracked, can cause conflicts
}
```

**After (Fast, No Conflicts)**:

```csharp
public async Task<GameSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
{
    var entity = await _dbContext.GameSessions
        .AsNoTracking() // ✅ Don't track for read-only queries
        .Include(gs => gs.User)
        .Include(gs => gs.Game)
        .FirstOrDefaultAsync(gs => gs.Id == id, ct);

    return entity?.ToDomain(); // ✅ No tracking conflicts
}
```

**When to Use AsNoTracking**:
- ✅ Read-only queries (GetById, GetAll, Search)
- ✅ Queries where data will be mapped to domain models
- ❌ Queries where EF Core needs to track changes for updates

### Pattern 4: Fix Repository UpdateAsync Tracking Conflicts

**Current Implementation** (Causes Conflicts):

```csharp
public async Task UpdateAsync(GameSession session, CancellationToken ct = default)
{
    var entity = session.ToEntity();
    _dbContext.GameSessions.Update(entity); // ❌ Throws if entity already tracked
    await _dbContext.SaveChangesAsync(ct);
}
```

**Fixed Implementation** (Defensive Approach):

```csharp
public async Task UpdateAsync(GameSession session, CancellationToken ct = default)
{
    var entity = session.ToEntity();

    // Detach existing tracked entity if present (defensive programming)
    var tracked = _dbContext.ChangeTracker.Entries<GameSessionEntity>()
        .FirstOrDefault(e => e.Entity.Id == entity.Id);

    if (tracked != null)
    {
        tracked.State = EntityState.Detached;
    }

    _dbContext.GameSessions.Update(entity); // ✅ Now safe
    await _dbContext.SaveChangesAsync(ct);
}
```

**Alternative: Attach + Modify Approach**:

```csharp
public async Task UpdateAsync(GameSession session, CancellationToken ct = default)
{
    var entity = session.ToEntity();

    // Attach entity and mark as modified
    _dbContext.Attach(entity);
    _dbContext.Entry(entity).State = EntityState.Modified;

    await _dbContext.SaveChangesAsync(ct);
}
```

**Which to Use?**:
- **Detach approach**: More defensive, handles existing tracked entities
- **Attach approach**: Cleaner, assumes no prior tracking

**Recommendation**: Use **Detach approach** to fix current known issues.

### Pattern 5: Container Reuse (Local Development)

**Setup** (One-time):

```bash
# Create ~/.testcontainers.properties
echo "testcontainers.reuse.enable=true" > ~/.testcontainers.properties
```

**Enable in Fixture**:

```csharp
public DatabaseFixture()
{
    var isLocalDev = Environment.GetEnvironmentVariable("CI") == null;

    var builder = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("meepleai_test")
        .WithUsername("testuser")
        .WithPassword("testpass");

    if (isLocalDev)
    {
        builder.WithReuse(true); // ✅ Only in local dev
    }

    _container = builder.Build();
}
```

**Performance**:
- First run: ~10s (container startup + migrations)
- Subsequent runs: ~200ms (connection only)
- **50x improvement** for iterative development

**⚠️ IMPORTANT**: NEVER enable in CI/CD (security risk, state leakage)

---

## Performance Benchmarks

### Current State (No Optimizations)

```
Test Suite: 15 test classes, 162 tests
├─ Container Startup: 15 × 2.5s = 37.5s
├─ Database Resets: 162 × 75ms = 12.2s
├─ Test Execution: 162 × 50ms = 8.1s
└─ Total Time: ~58 seconds
```

### After Priority 1+2 (Shared Fixture + Respawn)

```
Test Suite: 15 test classes, 162 tests
├─ Container Startup: 1 × 2.5s = 2.5s (shared)
├─ Database Resets: 162 × 15ms = 2.4s (Respawn)
├─ Test Execution: 162 × 50ms = 8.1s
└─ Total Time: ~13 seconds (4.5x faster)
```

### After All Optimizations

```
Test Suite: 15 test classes, 162 tests
├─ Container Startup: 1 × 2.5s = 2.5s (shared, reused locally)
├─ Database Resets: 162 × 10ms = 1.6s (Respawn.Postgres)
├─ Test Execution: 162 × 35ms = 5.7s (AsNoTracking)
├─ Parallel Execution: ÷2 (2 collections)
└─ Total Time: ~5 seconds (11.6x faster)
```

**Local Development** (with container reuse):
```
First Run: ~13 seconds
Subsequent Runs: ~3-4 seconds (30x faster for iterations)
```

---

## Migration Guide

### Step 1: Add Respawn Package

```bash
cd apps/api/tests/Api.Tests
dotnet add package Respawn.Postgres --version 6.2.1
```

### Step 2: Create DatabaseFixture

Create `apps/api/tests/Api.Tests/Infrastructure/DatabaseFixture.cs` using **Pattern 1** above.

### Step 3: Define Database Collection

Create `apps/api/tests/Api.Tests/Infrastructure/DatabaseCollection.cs`:

```csharp
[CollectionDefinition(nameof(DatabaseCollection))]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
```

### Step 4: Migrate Existing Tests

**For Repository Tests**:

1. Add `[Collection(nameof(DatabaseCollection))]` attribute
2. Replace `IAsyncLifetime` implementation with `InitializeTestAsync()` pattern
3. Inject `DatabaseFixture` via constructor

**Example Migration**:

```diff
// Before
- public class UserRepositoryTests : IntegrationTestBase<UserRepository>, IAsyncLifetime
+ [Collection(nameof(DatabaseCollection))]
+ public class UserRepositoryTests
  {
+     private readonly DatabaseFixture _fixture;
+     private MeepleAiDbContext _dbContext = null!;
+     private UserRepository _repository = null!;
+
+     public UserRepositoryTests(DatabaseFixture fixture)
+     {
+         _fixture = fixture;
+     }
+
+     private async Task InitializeTestAsync()
+     {
+         await _fixture.ResetDatabaseAsync();
+         _dbContext = _fixture.CreateDbContext();
+         _repository = new UserRepository(_dbContext);
+     }

      [Fact]
      public async Task AddAsync_WithValidUser_SavesSuccessfully()
      {
          // Arrange
+         await InitializeTestAsync();
-         await ResetDatabaseAsync();
          var user = CreateTestUser();

          // ... rest of test
      }
  }
```

### Step 5: Apply AsNoTracking Pattern

Update repository query methods:

```diff
  public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
  {
      return await _dbContext.Set<TEntity>()
+         .AsNoTracking()
          .FirstOrDefaultAsync(e => e.Id == id, ct);
  }
```

### Step 6: Fix UpdateAsync Tracking Conflicts

Apply **Pattern 4** (Detach approach) to all repository `UpdateAsync` methods:

```diff
  public async Task UpdateAsync(TDomain entity, CancellationToken ct = default)
  {
      var dbEntity = entity.ToEntity();
+
+     // Detach existing tracked entity if present
+     var tracked = _dbContext.ChangeTracker.Entries<TEntity>()
+         .FirstOrDefault(e => e.Entity.Id == dbEntity.Id);
+     if (tracked != null)
+         tracked.State = EntityState.Detached;
+
      _dbContext.Set<TEntity>().Update(dbEntity);
      await _dbContext.SaveChangesAsync(ct);
  }
```

### Step 7: Verify Tests Pass

```bash
cd apps/api
dotnet test --filter "Category=Integration"
```

### Step 8: Measure Performance Improvement

```bash
# Before optimization
time dotnet test

# After optimization
time dotnet test
```

### Step 9: Update Documentation

Update `docs/02-development/testing/testing-guide.md` with new patterns.

---

## Advanced Optimizations

### 1. Multiple Database Collections

For truly independent test groups (e.g., Authentication vs. GameManagement), create separate collections:

```csharp
[CollectionDefinition(nameof(AuthenticationDatabaseCollection))]
public class AuthenticationDatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}

[CollectionDefinition(nameof(GameManagementDatabaseCollection))]
public class GameManagementDatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
```

**Benefits**:
- Each collection gets its own database container
- Collections run in parallel
- 2 collections = 2x parallelism

**Trade-offs**:
- More containers = more resource usage
- Diminishing returns beyond 2-3 collections

### 2. Snapshot-Based Reset (Advanced)

For very large test suites, consider PostgreSQL template databases:

```csharp
// Create template database once
CREATE DATABASE template_meepleai WITH IS_TEMPLATE = true;

// Reset by dropping and recreating from template
DROP DATABASE IF EXISTS meepleai_test;
CREATE DATABASE meepleai_test TEMPLATE template_meepleai;
```

**Performance**: ~5-10ms for full database reset (faster than Respawn for large schemas)

**Trade-offs**: More complex setup, requires superuser privileges

### 3. Read-Only Test Collection

For tests that only read data (no writes), use a separate collection with pre-seeded data:

```csharp
[CollectionDefinition(nameof(ReadOnlyDatabaseCollection))]
public class ReadOnlyDatabaseCollection : ICollectionFixture<ReadOnlyDatabaseFixture>
{
}

public class ReadOnlyDatabaseFixture : DatabaseFixture
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Seed test data once
        await SeedTestDataAsync();
    }

    // No ResetDatabaseAsync() - read-only tests don't need cleanup
}
```

**Benefits**:
- No database reset overhead
- Fastest possible test execution
- Good for query/search tests

**Requirements**:
- Tests must not modify data
- Tests must be idempotent

---

## Common Pitfalls

### 1. ❌ Forgetting to Reset Database Between Tests

**Problem**:
```csharp
[Fact]
public async Task Test1()
{
    // ❌ No reset - uses data from previous test
    var user = await _repository.GetByEmailAsync("test@example.com");
}
```

**Solution**:
```csharp
[Fact]
public async Task Test1()
{
    await InitializeTestAsync(); // ✅ Reset before each test
    var user = await _repository.GetByEmailAsync("test@example.com");
}
```

### 2. ❌ Reusing DbContext Across Tests

**Problem**:
```csharp
private readonly MeepleAiDbContext _dbContext; // ❌ Shared across tests

public TestClass(DatabaseFixture fixture)
{
    _dbContext = fixture.CreateDbContext(); // ❌ Created once
}
```

**Solution**:
```csharp
private MeepleAiDbContext _dbContext = null!; // ✅ Recreated per test

private async Task InitializeTestAsync()
{
    await _fixture.ResetDatabaseAsync();
    _dbContext = _fixture.CreateDbContext(); // ✅ Fresh context
}
```

### 3. ❌ Enabling Container Reuse in CI/CD

**Problem**:
```csharp
.WithReuse(true) // ❌ ALWAYS enabled, even in CI
```

**Solution**:
```csharp
var isLocalDev = Environment.GetEnvironmentVariable("CI") == null;
if (isLocalDev)
{
    builder.WithReuse(true); // ✅ Only in local dev
}
```

### 4. ❌ Not Disposing DbContext

**Problem**:
```csharp
[Fact]
public async Task Test1()
{
    var context = _fixture.CreateDbContext();
    // ❌ Never disposed - connection leak
}
```

**Solution**:
```csharp
[Fact]
public async Task Test1()
{
    await using var context = _fixture.CreateDbContext(); // ✅ Auto-dispose
    // ... test code
}
```

### 5. ❌ Complex Test Dependencies in Same Collection

**Problem**:
```csharp
[Collection(nameof(DatabaseCollection))]
public class Test1 { } // Writes to Users table

[Collection(nameof(DatabaseCollection))]
public class Test2 { } // Depends on Users from Test1 ❌
```

**Solution**: Separate collections or ensure proper reset between tests.

---

## Monitoring & Debugging

### Enable Verbose Logging

```csharp
public DatabaseFixture()
{
    _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithLogger(ConsoleLogger.Instance) // ✅ See container logs
        .Build();
}
```

### Measure Test Performance

Use xUnit test output to track performance:

```csharp
private readonly ITestOutputHelper _output;

public UserRepositoryTests(DatabaseFixture fixture, ITestOutputHelper output)
{
    _fixture = fixture;
    _output = output;
}

[Fact]
public async Task Test1()
{
    var sw = Stopwatch.StartNew();
    await InitializeTestAsync();
    _output.WriteLine($"Database reset: {sw.ElapsedMilliseconds}ms");

    // ... test code

    _output.WriteLine($"Total test time: {sw.ElapsedMilliseconds}ms");
}
```

### Benchmark Respawn Performance

```csharp
[Fact]
public async Task Benchmark_DatabaseReset()
{
    var iterations = 10;
    var times = new List<long>();

    for (int i = 0; i < iterations; i++)
    {
        var sw = Stopwatch.StartNew();
        await _fixture.ResetDatabaseAsync();
        sw.Stop();
        times.Add(sw.ElapsedMilliseconds);
    }

    _output.WriteLine($"Average reset time: {times.Average()}ms");
    _output.WriteLine($"Min: {times.Min()}ms, Max: {times.Max()}ms");
}
```

---

## CI/CD Considerations

### GitHub Actions Configuration

```yaml
# .github/workflows/ci.yml
jobs:
  test-backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      # Ensure Docker is available (GitHub Actions has it by default)
      - name: Verify Docker
        run: docker --version

      # Disable container reuse in CI
      - name: Run Integration Tests
        run: |
          cd apps/api
          dotnet test --filter "Category=Integration" --logger "console;verbosity=detailed"
        env:
          CI: true # ✅ Disables container reuse
          TESTCONTAINERS_RYUK_DISABLED: false # ✅ Enable cleanup
```

### Optimize CI Test Execution

```yaml
# Run unit and integration tests in parallel
- name: Run Unit Tests
  run: dotnet test --filter "Category=Unit" &

- name: Run Integration Tests
  run: dotnet test --filter "Category=Integration" &

- name: Wait for tests
  run: wait
```

---

## Resources

### Official Documentation

- **Testcontainers for .NET**: https://dotnet.testcontainers.org/
- **Testcontainers Best Practices**: https://dotnet.testcontainers.org/api/best_practices/
- **xUnit Shared Context**: https://xunit.net/docs/shared-context
- **Respawn GitHub**: https://github.com/jbogard/Respawn
- **Respawn.Postgres**: https://github.com/sandord/Respawn.Postgres
- **EF Core AsNoTracking**: https://learn.microsoft.com/en-us/ef/core/querying/tracking

### Articles & Guides (2024-2025)

- **Milan Jovanović - Testcontainers Best Practices**: https://www.milanjovanovic.tech/blog/testcontainers-best-practices-dotnet-integration-testing
- **Khalid Abuhakmeh - Faster Database Tests with Respawn**: https://khalidabuhakmeh.com/faster-dotnet-database-integration-tests-with-respawn-and-xunit
- **Production Ready - Integration Testing with Testcontainers**: https://www.production-ready.de/2024/04/27/integration-testing-with-testcontainers-en.html
- **Paweł Pluta - Optimise Testcontainers Performance**: https://pawelpluta.com/optimise-testcontainers-for-better-tests-performance/

### MeepleAI-Specific Documentation

- **Testing Guide**: `docs/02-development/testing/testing-guide.md`
- **Test Architecture**: `apps/api/tests/Api.Tests/TEST_ARCHITECTURE.md`
- **Known Issues**: `docs/07-project-management/tracking/integration-tests-known-issues.md`

---

## Summary

**Quick Wins** (Implement First):
1. ✅ **Shared Database Fixture** (50-70% faster)
2. ✅ **Respawn.Postgres** (3-13x faster resets)
3. ✅ **AsNoTracking Pattern** (30% faster queries, fixes conflicts)

**Long-Term Optimizations**:
4. ✅ **Container Reuse** (Local dev: 50x faster iterations)
5. ✅ **Multiple Collections** (2x parallelism)
6. ✅ **Fix Repository UpdateAsync** (Resolves 5/15 failing tests)

**Expected Results**:
- CI/CD: 58s → 13s (4.5x faster)
- Local Dev: 58s → 3-4s with reuse (15-20x faster)
- Test Coverage: 67% → 100% (fix tracking conflicts)

**Next Steps**:
1. Review this guide with the team
2. Implement Priority 1+2 (Shared Fixture + Respawn)
3. Migrate existing tests incrementally
4. Measure and document performance improvements
5. Update testing documentation

---

**Version**: 1.0
**Last Updated**: 2025-11-16
**Maintainer**: Backend Team
**Status**: Ready for Implementation
