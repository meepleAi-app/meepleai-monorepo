# Testcontainers Best Practices

**Issue Reference**: [#2474 - Fix Testcontainers infrastructure stability issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2474)

## Overview

This guide provides best practices for using Testcontainers in the MeepleAI monorepo to ensure reliable, fast, and maintainable integration tests.

---

## Quick Start

### Local Development

**Recommended Approach**: Use shared containers with database isolation

```csharp
[Collection("SharedTestcontainers")]
public sealed class UserRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext _dbContext = null!;

    public UserRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_userrepo_{Guid.NewGuid():N}";
        var connString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Setup DbContext and run migrations
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connString)
            .Options;

        var mediator = TestDbContextFactory.CreateMockMediator();
        var eventCollector = TestDbContextFactory.CreateMockEventCollector();
        _dbContext = new MeepleAiDbContext(options, mediator.Object, eventCollector.Object);

        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingUser_ReturnsUser()
    {
        // Your test code here
    }
}
```

### CI/CD Environment

**Recommended Approach**: Use external infrastructure via environment variables

```bash
# Set these environment variables in your CI/CD pipeline
TEST_POSTGRES_CONNSTRING="Host=postgres.ci;Port=5432;Database=test;Username=postgres;Password=testpass"
TEST_REDIS_CONNSTRING="redis.ci:6379"
```

The `SharedTestcontainersFixture` will automatically detect these variables and use external infrastructure instead of starting local containers, resulting in **significantly faster test execution** in CI.

---

## Architecture

### Shared Container Pattern

**Benefits**:
- ✅ **95% faster**: Single container startup (~5s) vs per-test containers (~10s each)
- ✅ **No port conflicts**: Containers managed centrally
- ✅ **Reduced resource usage**: Less CPU/memory/disk I/O
- ✅ **Better isolation**: Database-level separation prevents cross-test pollution

**How It Works**:
1. **Single PostgreSQL container** shared across all test classes
2. **Unique database per test class** for isolation
3. **Cleanup via database drop** instead of container recreation
4. **xUnit collection** ensures sequential execution within collection

### Container Lifecycle

```
Test Suite Start
├─ SharedTestcontainersFixture.InitializeAsync()
│  ├─ Check for TEST_POSTGRES_CONNSTRING (CI environment)
│  ├─ If not found: Start shared PostgreSQL container
│  ├─ Retry logic: 3 attempts with exponential backoff (2s → 4s → 8s)
│  └─ Wait for container readiness (TCP health check)
│
├─ Test Class 1 Initialize
│  ├─ CreateIsolatedDatabaseAsync("test_auth_abc123")
│  ├─ Run EF Core migrations
│  └─ Execute tests
│
├─ Test Class 1 Dispose
│  └─ DropIsolatedDatabaseAsync("test_auth_abc123")
│
├─ Test Class 2, 3, N... (same pattern)
│
└─ Test Suite End
   └─ SharedTestcontainersFixture.DisposeAsync()
      ├─ Stop container gracefully
      └─ Cleanup Docker resources
```

---

## Configuration

### xUnit Configuration

**File**: `apps/api/tests/Api.Tests/xunit.runner.json`

```json
{
  "parallelizeAssembly": true,           // Run test collections in parallel
  "parallelizeTestCollections": true,    // Run tests within collection in parallel
  "maxParallelThreads": 8,               // Limit concurrency (adjust for CI)
  "methodTimeout": 60000,                // 1 minute per test (fail-fast)
  "longRunningTestSeconds": 20           // Report slow tests >20s
}
```

**Tuning Guidelines**:
- **Local Development**: `maxParallelThreads: 4-8` (based on CPU cores)
- **CI Environment**: `maxParallelThreads: 2-4` (shared runners)
- **methodTimeout**: Reduce if tests consistently complete faster

### Connection Strings

**PostgreSQL** (with optimizations):
```
Host=localhost;Port={port};Database={name};Username=postgres;Password=postgres;
Ssl Mode=Disable;Trust Server Certificate=true;KeepAlive=30;Pooling=false;Connection Timeout=10;
```

**Redis** (with optimizations):
```
localhost:{port},abortConnect=false,connectTimeout=10000,syncTimeout=10000,connectRetry=3
```

**Key Parameters**:
- `Pooling=false`: Prevents connection pool exhaustion in tests
- `Connection Timeout=10`: Increased from 5s for stability (Issue #2474)
- `connectRetry=3`: Automatic retry for transient Redis failures

---

## Troubleshooting

### Common Issues

#### 1. Port Conflicts

**Symptom**:
```
Exception: Port 5432 is already in use
```

**Solutions**:
1. **Run cleanup script**:
   ```powershell
   pwsh tools/cleanup/cleanup-testcontainers.ps1
   ```

2. **Check for running containers**:
   ```bash
   docker ps --filter "label=org.testcontainers=true"
   ```

3. **Manual cleanup**:
   ```bash
   docker rm -f $(docker ps -a --filter "label=org.testcontainers=true" -q)
   ```

#### 2. Orphaned Containers

**Symptom**:
```
Warning: Found 226 orphaned Testcontainer(s)
```

**Prevention**:
- ✅ Always use `[Collection("SharedTestcontainers")]` for integration tests
- ✅ Implement `IAsyncLifetime` and call `DropIsolatedDatabaseAsync()`
- ✅ Run cleanup script before test sessions

**Cleanup**:
```powershell
# Windows
pwsh tools/cleanup/cleanup-testcontainers.ps1

# Linux/macOS
bash tools/cleanup/cleanup-testcontainers.sh
```

#### 3. Container Startup Failures

**Symptom**:
```
PostgreSQL container failed to start after 3 attempts
Last error: Connection refused
Container ID: abc123def456
```

**Diagnostics**:
1. **Check Docker status**:
   ```bash
   docker info
   ```

2. **Verify Docker Desktop is running** (Windows/macOS)

3. **Check port availability**:
   ```powershell
   # PowerShell
   Test-NetConnection -ComputerName localhost -Port 5432

   # Bash
   nc -zv localhost 5432
   ```

4. **Review container logs**:
   ```bash
   docker logs <container-id>
   ```

**Retry Logic** (automatic as of Issue #2474):
- 3 attempts with exponential backoff (2s → 4s → 8s)
- Automatic cleanup of failed containers before retry
- Detailed diagnostics on final failure

#### 4. Migration Failures

**Symptom**:
```
Npgsql.NpgsqlException: relation "Users" does not exist
```

**Causes**:
- Migration not applied during test initialization
- Database dropped mid-test
- Incorrect connection string

**Solution**:
```csharp
// Ensure migrations in InitializeAsync()
await _dbContext.Database.MigrateAsync();

// Verify connection before tests
var canConnect = await _dbContext.Database.CanConnectAsync();
Assert.True(canConnect, "Database connection failed");
```

#### 5. Slow Test Execution

**Symptom**:
```
Test suite takes >10 minutes (expected <5 minutes)
```

**Optimizations**:

1. **Use External Infrastructure in CI**:
   ```yaml
   # .github/workflows/ci.yml
   services:
     postgres:
       image: postgres:16-alpine
       # ... health checks ...
   env:
     TEST_POSTGRES_CONNSTRING: "Host=localhost;Port=5432;..."
   ```

2. **Reduce Parallel Threads**:
   ```json
   // xunit.runner.json
   "maxParallelThreads": 4  // Lower if tests compete for resources
   ```

3. **Check for Resource Contention**:
   ```bash
   # Monitor CPU/memory during tests
   docker stats
   ```

---

## Best Practices

### DO ✅

- **Use `SharedTestcontainersFixture`** for all new integration tests
- **Create unique database names** with GUID: `test_auth_{Guid.NewGuid():N}`
- **Run cleanup script** before starting test sessions
- **Set `TEST_POSTGRES_CONNSTRING`** in CI for faster execution
- **Implement `IAsyncLifetime`** for proper setup/teardown
- **Use `[Collection("SharedTestcontainers")]`** attribute on test classes
- **Drop databases in `DisposeAsync()`** to prevent leaks

### DON'T ❌

- **Don't use `IntegrationTestBase<TRepository>`** for new tests (legacy pattern)
- **Don't share database names** across test classes
- **Don't forget to dispose `DbContext`** before dropping database
- **Don't skip cleanup scripts** after test failures
- **Don't hardcode connection strings** (use `SharedTestcontainersFixture` properties)
- **Don't modify shared container state** (use isolated databases)
- **Don't exceed 60s per test** (fail-fast configured in xUnit)

---

## Performance Metrics

### Before Optimizations (Issue #2474 Investigation)

- **Total tests**: 5,688
- **Failed tests**: 676 (11.9% failure rate)
- **Integration test pass rate**: ~53%
- **Orphaned containers**: 226+
- **Average suite time**: 15+ minutes

### After Optimizations (Expected)

- **Total tests**: 5,688
- **Failed tests**: <114 (<2% failure rate) ✅
- **Integration test pass rate**: >98% ✅
- **Orphaned containers**: 0 ✅
- **Average suite time**: <10 minutes ✅

---

## Migration Guide

### From `IntegrationTestBase<TRepository>` to `SharedTestcontainersFixture`

**Before** (Legacy):
```csharp
public class UserRepositoryTests : IntegrationTestBase<UserRepository>
{
    protected override string DatabaseName => "meepleai_user_test";

    protected override UserRepository CreateRepository(MeepleAiDbContext db)
        => new UserRepository(db, MockEventCollector.Object);

    [Fact]
    public async Task SomeTest()
    {
        await ResetDatabaseAsync();  // Expensive truncation
        // Test code
    }
}
```

**After** (Modern):
```csharp
[Collection("SharedTestcontainers")]
public sealed class UserRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext _dbContext = null!;
    private UserRepository _repository = null!;

    public UserRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_userrepo_{Guid.NewGuid():N}";
        var connString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connString)
            .Options;

        var mediator = TestDbContextFactory.CreateMockMediator();
        var eventCollector = TestDbContextFactory.CreateMockEventCollector();
        _dbContext = new MeepleAiDbContext(options, mediator.Object, eventCollector.Object);

        await _dbContext.Database.MigrateAsync();

        _repository = new UserRepository(_dbContext, eventCollector.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task SomeTest()
    {
        // No ResetDatabaseAsync() needed - fresh DB per test class
        // Test code
    }
}
```

**Benefits**:
- ⚡ **10x faster**: No per-test container recreation
- 🛡️ **Better isolation**: Fresh database per test class
- 🔧 **Less boilerplate**: No custom `CreateRepository()` method
- 📦 **Cleaner code**: Explicit dependency injection

---

## CI/CD Integration

### GitHub Actions Example

```yaml
backend:
  name: Backend - Build & Test
  runs-on: ubuntu-latest

  services:
    postgres:
      image: postgres:16-alpine
      env:
        POSTGRES_USER: postgres
        POSTGRES_PASSWORD: testpass
        POSTGRES_DB: meepleai_test
      options: >-
        --health-cmd pg_isready
        --health-interval 5s
        --health-timeout 5s
        --health-retries 10
      ports:
        - 5432:5432

    redis:
      image: redis:7-alpine
      options: >-
        --health-cmd "redis-cli ping"
        --health-interval 3s
        --health-timeout 3s
        --health-retries 10
      ports:
        - 6379:6379

  steps:
    - name: Pre-Test Cleanup
      shell: pwsh
      run: |
        # Cleanup orphaned containers from previous runs
        docker ps -a --filter "label=org.testcontainers=true" -q | \
          ForEach-Object { docker rm -f $_ 2>$null }

    - name: Wait for Services
      timeout-minutes: 2
      run: |
        until pg_isready -h localhost -p 5432; do sleep 1; done
        until redis-cli -h localhost ping | grep PONG; do sleep 1; done

    - name: Run Tests
      env:
        TEST_POSTGRES_CONNSTRING: "Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=testpass"
        TEST_REDIS_CONNSTRING: "localhost:6379"
      run: dotnet test --filter "Category=Integration"
```

---

## References

- **Issue #2474**: Testcontainers infrastructure stability fixes
- **Issue #2449**: Testcontainers cleanup automation
- **Issue #2031**: Docker exec hijacking workaround
- **Issue #1820**: Test suite performance optimization

---

**Last Updated**: 2026-01-15
**Maintainer**: MeepleAI Testing Team
