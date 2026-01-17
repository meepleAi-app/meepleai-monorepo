# Integration Test Performance Optimization

**Issue #2541**: Reduce backend integration test execution time from >11 minutes to <3 minutes

---

## Problem Analysis

### Current Bottlenecks (>11 minutes total)

1. **Testcontainers Sequential Startup**: ~350s
   - Each test run starts PostgreSQL + Redis containers
   - Container initialization: ~30-45s
   - Wait for readiness: ~5-10s

2. **EF Core Migrations**: ~80s
   - Database schema created for each container
   - Migration execution: ~8s per container

3. **Sequential Test Execution**: No parallelization
   - `DisableTestParallelization = true` globally
   - Tests run one at a time instead of concurrently

4. **Resource Overhead**: Memory/CPU contention
   - Multiple containers running simultaneously during test class overlap

---

## Recommended Solution: External Infrastructure

### Architecture

```
Docker Compose Infrastructure (persistent)
├── PostgreSQL 16 (port 5432)
├── Redis 7 (port 6379)
└── Qdrant (optional, port 6333)

Test Execution (parallel)
├── maxParallelThreads: 4-8
├── Shared container connections via ENV vars
├── Database isolation per test class
└── Transaction rollback for cleanup
```

### Performance Impact

| Component | Testcontainers | External Infra | Improvement |
|-----------|---------------|----------------|-------------|
| Container Startup | ~350s | 0s (already running) | -100% |
| Database Migrations | ~80s | ~40s (parallel) | -50% |
| Test Parallelization | 1x | 4-8x | +400-800% |
| **Total Time** | **11+ min** | **<3 min** | **73% faster** |

---

## Implementation Steps

### Step 1: Start Infrastructure

```bash
# Terminal 1: Start persistent test infrastructure
cd infra
docker compose up -d postgres redis

# Verify services are running
docker compose ps
docker compose logs postgres | tail -20
docker compose logs redis | tail -20
```

### Step 2: Configure Environment Variables

```powershell
# PowerShell (Windows)
$env:TEST_POSTGRES_CONNSTRING="Host=localhost;Port=5432;Database=test_shared;Username=admin;Password=<from-infra/secrets/admin.secret>;Ssl Mode=Disable;Trust Server Certificate=true"
$env:TEST_REDIS_CONNSTRING="localhost:6379"

# Verify
echo $env:TEST_POSTGRES_CONNSTRING
```

```bash
# Bash (Linux/Mac)
export TEST_POSTGRES_CONNSTRING="Host=localhost;Port=5432;Database=test_shared;Username=admin;Password=<from-secret>;Ssl Mode=Disable"
export TEST_REDIS_CONNSTRING="localhost:6379"
```

### Step 3: Enable Parallel Execution

**Remove global disable** (AssemblyInfo.cs):
```csharp
// Before
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// After
// [assembly: CollectionBehavior(DisableTestParallelization = true)] // REMOVED for Issue #2541
```

**Configure xUnit** (xunit.runner.json):
```json
{
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 4,          // 4 for local, 8 for CI with more resources
  "methodTimeout": 60000,
  "longRunningTestSeconds": 20
}
```

### Step 4: Run Optimized Tests

```bash
cd apps/api/tests/Api.Tests

# Method 1: Using xUnit configuration (4 threads)
dotnet test --no-build --logger "console;verbosity=minimal"

# Method 2: Explicit parallelization (8 threads)
dotnet test --no-build --parallel --max-cpu-count 8

# Method 3: Integration tests only
dotnet test --no-build --filter "Category=Integration"
```

---

## Test Isolation Strategy

### Current Approach (SharedTestcontainersFixture)

```csharp
[Collection("SharedTestcontainers")]
public class MyIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName;
    private MeepleAiDbContext _dbContext;

    public async Task InitializeAsync()
    {
        // Create ISOLATED database for this test class
        var connString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = CreateDbContext(connString);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }
}
```

**Isolation Mechanism**:
- ONE shared PostgreSQL container
- UNIQUE database per test class (`test_context_{guid}`)
- Database drop after test class completion
- No shared state between test classes

### Alternative: Transaction-Based Isolation

```csharp
public abstract class TransactionTestBase : IClassFixture<PostgresFixture>
{
    private IDbContextTransaction _transaction;

    public async Task InitializeAsync()
    {
        _transaction = await DbContext.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();  // Cleanup in ~10ms
    }
}
```

**Pros**: Faster cleanup (10ms vs 100ms for database drop)
**Cons**: Doesn't work with multiple DbContext instances, some operations can't rollback

---

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Backend Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_USER: postgres
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: test_shared
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

      redis:
        image: redis:7-alpine
        ports:
          - 6379:6379
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore apps/api/src/Api

      - name: Build
        run: dotnet build apps/api/src/Api --no-restore

      - name: Run Tests (Parallel)
        run: |
          dotnet test apps/api/tests/Api.Tests \
            --no-build \
            --parallel \
            --max-cpu-count 8 \
            --logger "trx;LogFileName=test-results.trx"
        env:
          TEST_POSTGRES_CONNSTRING: "Host=localhost;Port=5432;Database=test_shared;Username=postgres;Password=postgres;Ssl Mode=Disable"
          TEST_REDIS_CONNSTRING: "localhost:6379"

      - name: Publish Test Results
        uses: dorny/test-reporter@v1
        if: always()
        with:
          name: Backend Tests
          path: '**/test-results.trx'
          reporter: dotnet-trx
```

---

## Performance Benchmarks

### Baseline (Before Optimization)

```
Configuration:
- Testcontainers per test class
- Sequential execution
- DisableTestParallelization = true

Results:
- Container startup: ~350s
- Migrations: ~80s
- Test execution: ~320s
- Total: 11+ minutes
```

### External Infrastructure (Target)

```
Configuration:
- Docker Compose persistent containers
- Parallel execution (maxParallelThreads=4)
- Shared container, isolated databases

Results:
- Container startup: 0s (already running)
- Migrations: ~40s (parallel across test classes)
- Test execution: ~80s (4x speedup)
- Total: <3 minutes ✅
```

### Testcontainers + Parallel (NOT Recommended)

```
Configuration:
- Testcontainers with AssemblyFixture
- Parallel collections
- Multiple collection instances

Results:
- Container startup: ~35s (1x shared)
- Migrations: ~8s (1x shared)
- Test execution: ~100s (parallel)
- Issues: Resource exhaustion, flaky tests, access violations
- Total: ~3 minutes BUT unstable ❌
```

---

## Best Practices

### DO ✅

1. **Use External Infrastructure Locally**
   - Start `docker compose up -d postgres redis`
   - Set environment variables
   - Run tests with `--parallel`

2. **Use GitHub Actions Services for CI**
   - Faster than Testcontainers in CI
   - No container startup overhead
   - Built-in health checks

3. **Isolate Test Data**
   - Unique database per test class
   - Transaction rollback where applicable
   - Clean up in DisposeAsync

4. **Configure Parallelism Appropriately**
   - Local: `maxParallelThreads: 4`
   - CI (4 CPU): `maxParallelThreads: 4`
   - CI (8 CPU): `maxParallelThreads: 8`

### DON'T ❌

1. **Don't use Testcontainers for local development**
   - Slower than persistent Docker Compose
   - Resource overhead from startup/shutdown
   - Complexity with parallel execution

2. **Don't exceed system CPU count**
   - Causes context switching overhead
   - May trigger resource exhaustion
   - Diminishing returns beyond 8 threads

3. **Don't share transactions across tests**
   - Transaction scope limited to single DbContext
   - DDL operations don't rollback
   - Parallel tests can't use same transaction

4. **Don't skip test isolation**
   - Always clean up test data
   - Use unique database names or transactions
   - Verify no cross-test contamination

---

## Troubleshooting

### Tests Still Slow (>5 minutes)

**Check**:
```bash
# Verify external infrastructure is running
docker compose ps postgres redis

# Check container health
docker compose logs postgres | grep "ready to accept connections"
docker compose logs redis | grep "Ready to accept connections"

# Verify environment variables are set
echo $env:TEST_POSTGRES_CONNSTRING  # PowerShell
printenv TEST_POSTGRES_CONNSTRING   # Bash
```

**Fix**:
- Restart Docker Compose if containers are unhealthy
- Increase Docker memory allocation (8GB+ recommended)
- Reduce maxParallelThreads if CPU-bound

### Access Violation Crashes

**Symptom**: `exit code -1073741819`

**Causes**:
- Too many parallel threads
- Docker resource limits
- Memory exhaustion

**Fix**:
```json
// Reduce parallelism in xunit.runner.json
{
  "maxParallelThreads": 2  // or 1 for debugging
}
```

### Test Failures Only in Parallel Mode

**Symptom**: Tests pass sequentially, fail in parallel

**Causes**:
- Shared static state
- Non-unique test data
- Race conditions

**Fix**:
```csharp
// Use unique identifiers
var userId = Guid.NewGuid();
var testKey = $"test:{Guid.NewGuid():N}";

// Avoid static fields
// ❌ private static List<User> _users;
// ✅ private List<User> _users;
```

---

## Migration Checklist

- [ ] Start Docker Compose infrastructure
- [ ] Set TEST_POSTGRES_CONNSTRING environment variable
- [ ] Set TEST_REDIS_CONNSTRING environment variable
- [ ] Remove `DisableTestParallelization = true` from AssemblyInfo.cs
- [ ] Configure `maxParallelThreads` in xunit.runner.json
- [ ] Run tests: `dotnet test --parallel`
- [ ] Verify execution time <3 minutes
- [ ] Check for flaky tests (run 3x)
- [ ] Update CI/CD workflow with GitHub Actions services
- [ ] Document environment setup for team

---

## Expected Results

**Before**:
```
dotnet test
> Total: 11:23 minutes
> Testcontainers startup overhead: ~6 minutes
> Sequential execution: ~5 minutes
```

**After**:
```
dotnet test --parallel --max-cpu-count 4
> Total: 2:45 minutes ✅
> No container startup (external infra): 0s
> Parallel execution (4 threads): ~2:45 minutes
```

---

**Status**: Solution documented, ready for implementation
**Last Updated**: 2026-01-16
**Issue**: #2541
