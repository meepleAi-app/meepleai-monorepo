# Test Optimization Strategy
## Comprehensive Design for AdminTestCollection Parallelization

**Status**: Design Phase
**Created**: 2025-11-10
**Target**: 60-70% execution time reduction (from ~15min to ~5-7min)

---

## Executive Summary

### Current Bottlenecks
1. **Sequential Execution**: AdminTestCollection with `DisableParallelization = true` forces 150+ tests to run serially
2. **Password Hashing Overhead**: Each test performs PBKDF2 with 210K iterations (~100-200ms per user)
3. **Database Cleanup Overhead**: 15+ DELETE queries per test (~50-100ms per test)
4. **Container Initialization**: Each test class may create new DbContext instances

### Optimization Targets
- **Phase 1**: Enable safe parallelization (Target: 40% time reduction)
- **Phase 2**: Optimize database operations (Target: 20% time reduction)
- **Phase 3**: Optimize fixtures and containers (Target: 10% time reduction)

---

## 1. Parallelization Strategy

### 1.1 Problem Analysis

**Why DisableParallelization was added**:
```csharp
[CollectionDefinition("Admin Endpoints", DisableParallelization = true)]
public class AdminTestCollection :
    ICollectionFixture<PostgresCollectionFixture>,
    ICollectionFixture<WebApplicationFactoryFixture>
```

**Root causes requiring serialization**:
- Shared PostgresContainer across all tests in collection
- Potential database state conflicts (user creation, session management)
- Shared WebApplicationFactory instances

**Impact**:
- 150+ tests × ~6s avg = ~900s sequential (15 minutes)
- With parallelization: ~900s / 4 threads = ~225s (3.75 minutes) - **60% reduction**

### 1.2 Safe Parallelization Approach

#### Strategy: Collection-Level Isolation

**Principle**: Tests within different collections can run in parallel, but tests within AdminTestCollection remain safe through better isolation patterns.

**Implementation**:
```csharp
// BEFORE: Single monolithic collection
[CollectionDefinition("Admin Endpoints", DisableParallelization = true)]
public class AdminTestCollection : ICollectionFixture<PostgresCollectionFixture>

// AFTER: Multiple smaller collections with shared fixtures
[CollectionDefinition("Admin User Management")]  // No DisableParallelization
public class AdminUserMgmtCollection : ICollectionFixture<PostgresCollectionFixture>

[CollectionDefinition("Admin Session Management")]  // No DisableParallelization
public class AdminSessionMgmtCollection : ICollectionFixture<PostgresCollectionFixture>

[CollectionDefinition("Admin API Key Management")]  // No DisableParallelization
public class AdminApiKeyMgmtCollection : ICollectionFixture<PostgresCollectionFixture>

[CollectionDefinition("Admin Cache Management")]  // No DisableParallelization
public class AdminCacheMgmtCollection : ICollectionFixture<PostgresCollectionFixture>
```

**Key Insight**: xUnit v3 allows parallel execution ACROSS collections by default. We split the monolithic AdminTestCollection into domain-specific collections, each inheriting the same PostgresCollectionFixture (shared safely via xUnit's fixture management).

#### Test Categorization

| Category | Test Files | Safe for Parallel | Reason |
|----------|-----------|-------------------|--------|
| **User Management** | UserManagementEndpointsTests.cs | ✅ Yes | Each test creates unique users (TestRunId isolation) |
| **Session Management** | SessionManagementEndpointsTests.cs, SessionStatusEndpointsTests.cs | ✅ Yes | Session isolation per user, no cross-user conflicts |
| **API Key Management** | ApiKeyManagementEndpointsTests.cs, ApiKeyAuthenticationIntegrationTests.cs | ✅ Yes | API keys scoped to users, unique per test |
| **Cache Management** | CacheAdminEndpointsTests.cs, CacheInvalidationIntegrationTests.cs | ⚠️ Conditional | Shared cache requires namespace isolation |
| **Prompt Management** | PromptManagementEndpointsTests.cs | ✅ Yes | Prompt templates are user-scoped |
| **Config Management** | ConfigurationManagementEndpointsTests.cs (if exists) | ⚠️ Conditional | Shared configuration requires key isolation |
| **Auth Flows** | AuthEndpointsComprehensiveTests.cs, AuthorizationEdgeCasesIntegrationTests.cs | ✅ Yes | Unique users per test |

#### Isolation Guarantees

**Current Isolation Mechanisms** (already in place):
1. **TestRunId**: `Guid.NewGuid().ToString("N")[..8]` - ensures unique test identifiers
2. **Unique Emails**: `$"{username}-{TestRunId}@test.local"` - prevents user conflicts
3. **Scoped DbContext**: Each test creates its own DbContext via `Factory.Services.CreateScope()`
4. **Tracked Resources**: `_testUserIds`, `_testGameIds` lists for cleanup

**Additional Isolation Needed**:
1. **Cache Namespace Isolation**:
   ```csharp
   // Add test-specific cache prefix
   private string GetTestCacheKey(string key) => $"test:{TestRunId}:{key}";
   ```

2. **Configuration Key Isolation**:
   ```csharp
   // Use test-specific config keys
   private string GetTestConfigKey(string key) => $"Test:{TestRunId}:{key}";
   ```

### 1.3 Implementation Plan

#### Phase 1.1: Split AdminTestCollection (Week 1, Days 1-2)

**Tasks**:
1. Create new collection definitions:
   - `AdminUserMgmtCollection`
   - `AdminSessionMgmtCollection`
   - `AdminApiKeyMgmtCollection`
   - `AdminCacheMgmtCollection`
   - `AdminPromptMgmtCollection`

2. Migrate test files to new collections:
   ```csharp
   // BEFORE
   [Collection("Admin Endpoints")]
   public class UserManagementEndpointsTests : IntegrationTestBase

   // AFTER
   [Collection("Admin User Management")]
   public class UserManagementEndpointsTests : IntegrationTestBase
   ```

3. Run verification:
   ```bash
   # Verify no parallelization conflicts
   dotnet test --logger "console;verbosity=detailed" --blame-hang-timeout 5m
   ```

**Success Criteria**:
- All tests pass in parallel execution
- No deadlocks or timeouts
- No intermittent failures (run 5 times)

**Rollback**:
- Revert collection attribute changes
- Re-enable `DisableParallelization = true`

#### Phase 1.2: Add Cache Isolation (Week 1, Days 3-4)

**Implementation**:
```csharp
// IntegrationTestBase.cs
protected string GetTestCacheKey(string key) => $"test:{TestRunId}:{key}";

// CacheAdminEndpointsTests.cs
[Fact]
public async Task Stats_ReturnsCorrectMetrics()
{
    // Use isolated cache keys
    var cacheKey = GetTestCacheKey($"game:{gameId}");
    await CacheService.SetAsync(cacheKey, data);

    // ...test logic...
}
```

**Verification**:
```bash
# Run cache tests in parallel
dotnet test --filter "FullyQualifiedName~CacheAdminEndpointsTests" --parallel
```

#### Phase 1.3: Add Configuration Isolation (Week 1, Day 5)

**Implementation**:
```csharp
// IntegrationTestBase.cs
protected async Task<ConfigurationEntity> CreateTestConfigAsync(
    string key,
    string value,
    ConfigValueType valueType = ConfigValueType.String)
{
    var testKey = GetTestConfigKey(key);
    // ...create config with isolated key...
}
```

---

## 2. Database Optimization

### 2.1 Problem Analysis

**Current Cleanup Pattern** (per test):
```csharp
public async Task DisposeAsync()
{
    using var scope = Factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    // 15+ individual DELETE queries
    if (_testUserIds.Any())
    {
        await db.Users.Where(u => _testUserIds.Contains(u.Id)).ExecuteDeleteAsync();
    }
    if (_testSessionIds.Any())
    {
        await db.UserSessions.Where(s => _testSessionIds.Contains(s.Id)).ExecuteDeleteAsync();
    }
    // ...13 more similar queries...
}
```

**Overhead**:
- 15+ round-trips to database per test
- Each query: ~5-10ms network + query execution
- Total per test: ~75-150ms

**Impact**: 150 tests × 100ms cleanup = **15 seconds** of pure cleanup overhead

### 2.2 Batch Cleanup Strategy

#### Approach 1: Single SQL Script

**Implementation**:
```csharp
public async Task DisposeAsync()
{
    using var scope = Factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    // Single batch DELETE with CTEs
    var sql = @"
        WITH deleted_users AS (
            DELETE FROM users WHERE id = ANY(@userIds) RETURNING id
        ),
        deleted_sessions AS (
            DELETE FROM user_sessions WHERE user_id = ANY(@userIds)
        ),
        deleted_api_keys AS (
            DELETE FROM api_keys WHERE user_id = ANY(@userIds)
        ),
        deleted_chats AS (
            DELETE FROM chat_logs WHERE user_id = ANY(@userIds)
        ),
        deleted_games AS (
            DELETE FROM games WHERE id = ANY(@gameIds)
        ),
        deleted_rule_specs AS (
            DELETE FROM rule_specs WHERE game_id = ANY(@gameIds)
        ),
        deleted_pdfs AS (
            DELETE FROM pdf_documents WHERE game_id = ANY(@gameIds)
        ),
        deleted_vector_docs AS (
            DELETE FROM vector_documents WHERE game_id = ANY(@gameIds)
        ),
        deleted_prompts AS (
            DELETE FROM prompt_templates WHERE id = ANY(@promptIds)
        ),
        deleted_configs AS (
            DELETE FROM system_configurations WHERE id = ANY(@configIds)
        )
        SELECT
            (SELECT COUNT(*) FROM deleted_users) as users_deleted,
            (SELECT COUNT(*) FROM deleted_games) as games_deleted;
    ";

    await db.Database.ExecuteSqlRawAsync(sql,
        new NpgsqlParameter("@userIds", _testUserIds.ToArray()),
        new NpgsqlParameter("@gameIds", _testGameIds.ToArray()),
        new NpgsqlParameter("@promptIds", _testPromptIds.ToArray()),
        new NpgsqlParameter("@configIds", _testConfigIds.ToArray())
    );
}
```

**Benefits**:
- Single round-trip (15 → 1 queries)
- Atomic cleanup (all-or-nothing)
- Proper cascading via CTEs

**Expected Improvement**: ~100ms → ~15ms per test = **85ms savings × 150 tests = 12.75 seconds**

#### Approach 2: Transaction-Based Cleanup

**Implementation**:
```csharp
public async Task DisposeAsync()
{
    using var scope = Factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    using var transaction = await db.Database.BeginTransactionAsync();
    try
    {
        // Batch deletes in correct dependency order
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM user_sessions WHERE user_id = ANY(@userIds)",
            new NpgsqlParameter("@userIds", _testUserIds.ToArray()));

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM api_keys WHERE user_id = ANY(@userIds)",
            new NpgsqlParameter("@userIds", _testUserIds.ToArray()));

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM users WHERE id = ANY(@userIds)",
            new NpgsqlParameter("@userIds", _testUserIds.ToArray()));

        // ...other tables...

        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

**Benefits**:
- Fewer round-trips (15 → 5-7 queries)
- Transactional safety
- Easier to debug than complex CTE

**Expected Improvement**: ~100ms → ~30ms per test = **70ms savings × 150 tests = 10.5 seconds**

### 2.3 Shared Test User Pattern

**Problem**: Each test creates users and hashes passwords (210K PBKDF2 iterations)

**Current**:
```csharp
[Fact]
public async Task CreateUser_ReturnsSuccess()
{
    var user = await CreateTestUserAsync("admin", UserRole.Admin);  // ~150ms PBKDF2
    // ...test logic...
}
```

**Optimized Approach**: Pre-created Test Users

**Implementation**:
```csharp
// PostgresCollectionFixture.cs
public class PostgresCollectionFixture : IAsyncLifetime
{
    public UserEntity AdminUser { get; private set; } = null!;
    public UserEntity EditorUser { get; private set; } = null!;
    public UserEntity RegularUser { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // ...start Postgres, run migrations...

        // Pre-create shared test users (one-time cost)
        await using var context = CreateDbContext();

        AdminUser = new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = "admin@test.fixture",
            PasswordHash = HashPassword("Admin123!"),  // One-time 150ms
            DisplayName = "Test Admin",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        EditorUser = new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = "editor@test.fixture",
            PasswordHash = HashPassword("Editor123!"),  // One-time 150ms
            DisplayName = "Test Editor",
            Role = UserRole.Editor,
            CreatedAt = DateTime.UtcNow
        };

        RegularUser = new UserEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = "user@test.fixture",
            PasswordHash = HashPassword("User123!"),  // One-time 150ms
            DisplayName = "Test User",
            Role = UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(AdminUser, EditorUser, RegularUser);
        await context.SaveChangesAsync();
    }
}

// IntegrationTestBase.cs
protected UserEntity GetSharedUser(UserRole role)
{
    return role switch
    {
        UserRole.Admin => _postgresFixture.AdminUser,
        UserRole.Editor => _postgresFixture.EditorUser,
        UserRole.User => _postgresFixture.RegularUser,
        _ => throw new ArgumentException($"No shared user for role {role}")
    };
}
```

**When to use**:
- Read-only tests (GET endpoints, query operations)
- Tests that don't modify user state
- Authorization/authentication validation

**When NOT to use**:
- Tests that modify user properties (role changes, password updates)
- Tests requiring unique user attributes
- Tests that delete users

**Expected Improvement**:
- 100 tests use shared users → 100 × 150ms = **15 seconds saved**
- 50 tests still create unique users (state modification tests)

### 2.4 Database Seeding Optimization

**Current**: Each test creates fresh games, RuleSpecs, etc.

**Optimized**: Fixture-Level Seeding

**Implementation**:
```csharp
// PostgresCollectionFixture.cs
public GameEntity ChessGame { get; private set; } = null!;
public GameEntity TicTacToeGame { get; private set; } = null!;

public async ValueTask InitializeAsync()
{
    // ...migrations, shared users...

    // Pre-seed common test games
    await using var context = CreateDbContext();

    ChessGame = new GameEntity
    {
        Id = "chess-fixture",
        Name = "Chess",
        CreatedAt = DateTime.UtcNow
    };

    TicTacToeGame = new GameEntity
    {
        Id = "tic-tac-toe-fixture",
        Name = "Tic-Tac-Toe",
        CreatedAt = DateTime.UtcNow
    };

    context.Games.AddRange(ChessGame, TicTacToeGame);
    await context.SaveChangesAsync();
}
```

**Benefits**:
- Shared reference data across tests
- Reduced database writes
- Faster test setup

**Constraints**:
- Read-only access to shared games
- Tests requiring game modifications must create unique instances

---

## 3. Fixture Lifecycle Improvements

### 3.1 Current Fixture Architecture

```
PostgresCollectionFixture (Collection Fixture)
├── PostgreSqlContainer (Testcontainers)
├── ConnectionString
├── Configuration
└── CreateDbContext() - creates new context per call

WebApplicationFactoryFixture (Collection Fixture)
├── HttpClient
├── Factory (WebApplicationFactory<Program>)
└── Configured with test Postgres connection
```

**Issue**: Each test class in the collection gets the SAME fixture instance (collection fixture semantics), but tests within a class may create multiple DbContext instances unnecessarily.

### 3.2 Optimization: DbContext Pooling

**Implementation**:
```csharp
// PostgresCollectionFixture.cs
public class PostgresCollectionFixture : IAsyncLifetime
{
    private readonly ObjectPool<MeepleAiDbContext> _dbContextPool;

    public PostgresCollectionFixture()
    {
        _dbContextPool = new DefaultObjectPoolProvider()
            .Create(new DbContextPooledObjectPolicy(this));
    }

    public MeepleAiDbContext GetPooledDbContext()
    {
        return _dbContextPool.Get();
    }

    public void ReturnDbContext(MeepleAiDbContext context)
    {
        _dbContextPool.Return(context);
    }
}

// DbContextPooledObjectPolicy.cs
public class DbContextPooledObjectPolicy : IPooledObjectPolicy<MeepleAiDbContext>
{
    private readonly PostgresCollectionFixture _fixture;

    public DbContextPooledObjectPolicy(PostgresCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    public MeepleAiDbContext Create()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        return new MeepleAiDbContext(options);
    }

    public bool Return(MeepleAiDbContext obj)
    {
        // Reset context state before returning to pool
        obj.ChangeTracker.Clear();
        return true;
    }
}
```

**Usage**:
```csharp
// IntegrationTestBase.cs
protected async Task<T> WithDbContextAsync<T>(Func<MeepleAiDbContext, Task<T>> action)
{
    var context = _postgresFixture.GetPooledDbContext();
    try
    {
        return await action(context);
    }
    finally
    {
        _postgresFixture.ReturnDbContext(context);
    }
}
```

**Benefits**:
- Reduces DbContext construction overhead
- Reuses connection strings and configuration
- Automatic state cleanup via ChangeTracker.Clear()

**Expected Improvement**: 5-10ms per test (DbContext creation) × 150 tests = **0.75-1.5 seconds**

### 3.3 Container Initialization Optimization

**Current Timing** (from logs):
```
🚀 [PostgresCollectionFixture] Starting Postgres container...
✅ [PostgresCollectionFixture] Container started in 3247ms
🔧 [PostgresCollectionFixture] Running database migrations...
✅ [PostgresCollectionFixture] Database migrated successfully
```

**Optimization 1: Parallel Container Start**

```csharp
// If multiple test collections exist
public async ValueTask InitializeAsync()
{
    var postgresTask = StartPostgresAsync();
    var redisTask = StartRedisAsync();  // If Redis fixture exists
    var qdrantTask = StartQdrantAsync();  // If Qdrant fixture exists

    await Task.WhenAll(postgresTask, redisTask, qdrantTask);
}
```

**Optimization 2: Container Image Pre-Pull**

**GitHub Actions CI**:
```yaml
- name: Pre-pull test container images
  run: |
    docker pull postgres:15-alpine
    docker pull redis:7-alpine
    docker pull qdrant/qdrant:latest
```

**Benefit**: Eliminates ~10-20s image pull time in CI

**Optimization 3: Reuse Containers Across Test Runs (Local Dev)**

**Implementation**:
```csharp
public class PostgresCollectionFixture : IAsyncLifetime
{
    private const string ContainerLabel = "meepleai-test-postgres";

    private async Task StartPostgresAsync()
    {
        // Check if container already exists from previous run
        var existingContainer = await TryGetExistingContainerAsync(ContainerLabel);

        if (existingContainer != null && await IsContainerHealthy(existingContainer))
        {
            Console.WriteLine("♻️ [PostgresCollectionFixture] Reusing existing Postgres container");
            PostgresContainer = existingContainer;
            return;
        }

        // Create new container with label
        PostgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithLabel(ContainerLabel, "true")
            .WithReuse(true)  // Testcontainers feature
            .Build();

        await PostgresContainer.StartAsync();
    }
}
```

**Benefit (Local Dev)**:
- First run: 3-5s startup
- Subsequent runs: ~0.5s connection validation
- **Saves 2.5-4.5s per test run**

**CI Consideration**: Keep container recreation in CI for isolation

---

## 4. CI-Specific Optimizations

### 4.1 Docker Layer Caching

**GitHub Actions**:
```yaml
- name: Set up Docker Buildx
  uses: docker/setup-buildx-action@v2

- name: Cache Docker layers
  uses: actions/cache@v3
  with:
    path: /tmp/.buildx-cache
    key: ${{ runner.os }}-buildx-${{ hashFiles('**/Dockerfile') }}
    restore-keys: |
      ${{ runner.os }}-buildx-

- name: Pre-pull test container images
  run: |
    docker pull postgres:15-alpine || true
    docker pull redis:7-alpine || true
    docker pull qdrant/qdrant:latest || true
```

**Expected Improvement**: 10-20s saved per CI run

### 4.2 Test Sharding

**Strategy**: Split tests across multiple CI jobs

**GitHub Actions**:
```yaml
jobs:
  test:
    strategy:
      matrix:
        shard: [1, 2, 3, 4]
    steps:
      - name: Run tests (shard ${{ matrix.shard }}/4)
        run: |
          dotnet test \
            --filter "FullyQualifiedName!~ExcludedCategory" \
            --logger "console;verbosity=detailed" \
            -- RunConfiguration.TestSessionTimeout=600000 \
            Xunit.ParallelizeTestCollections=true \
            Xunit.MaxParallelThreads=2
```

**Benefits**:
- 4 parallel jobs → 4x throughput
- Each job runs ~3-4 minutes instead of 15 minutes
- **Total CI time: ~4 minutes (including setup overhead)**

### 4.3 Timeout Configurations

**Current**: Tests occasionally hang, causing 20-minute timeouts

**Optimized**:
```yaml
- name: Run API tests with timeout
  timeout-minutes: 10  # Reduced from default 20
  run: |
    dotnet test \
      --blame-hang-timeout 5m \
      --blame-hang-dump-type full \
      -- RunConfiguration.TestSessionTimeout=600000
```

**Benefit**: Faster failure detection, prevents wasted CI time

---

## 5. Performance Monitoring

### 5.1 Test Timing Instrumentation

**Implementation**:
```csharp
// IntegrationTestBase.cs
protected class TestTimingScope : IAsyncDisposable
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly string _testName;

    public TestTimingScope(string testName)
    {
        _testName = testName;
        Console.WriteLine($"⏱️ [{_testName}] Starting...");
    }

    public async ValueTask DisposeAsync()
    {
        _stopwatch.Stop();
        var elapsed = _stopwatch.ElapsedMilliseconds;
        var emoji = elapsed switch
        {
            < 1000 => "⚡",  // Fast
            < 3000 => "✅",  // Normal
            < 5000 => "⚠️",  // Slow
            _ => "🐌"        // Very Slow
        };

        Console.WriteLine($"{emoji} [{_testName}] Completed in {elapsed}ms");

        // Warn on slow tests
        if (elapsed > 5000)
        {
            Console.WriteLine($"⚠️ [{_testName}] Exceeded 5s threshold - consider optimization");
        }
    }
}

// Usage
[Fact]
public async Task CreateUser_ReturnsSuccess()
{
    await using var _ = new TestTimingScope(nameof(CreateUser_ReturnsSuccess));

    var user = await CreateTestUserAsync("admin", UserRole.Admin);
    // ...test logic...
}
```

**Output**:
```
⏱️ [CreateUser_ReturnsSuccess] Starting...
⚡ [CreateUser_ReturnsSuccess] Completed in 234ms
```

### 5.2 Bottleneck Detection

**Implementation**:
```csharp
// TestPerformanceMetrics.cs
public static class TestPerformanceMetrics
{
    private static readonly ConcurrentDictionary<string, List<long>> _timings = new();

    public static void RecordTiming(string testName, long milliseconds)
    {
        _timings.AddOrUpdate(
            testName,
            _ => new List<long> { milliseconds },
            (_, list) => { list.Add(milliseconds); return list; }
        );
    }

    public static void PrintSummary()
    {
        Console.WriteLine("\n📊 Test Performance Summary:");
        Console.WriteLine("─────────────────────────────────────────────");

        var sortedTests = _timings
            .Select(kvp => new
            {
                Test = kvp.Key,
                AvgMs = kvp.Value.Average(),
                MaxMs = kvp.Value.Max(),
                Runs = kvp.Value.Count
            })
            .OrderByDescending(x => x.AvgMs)
            .Take(10);

        foreach (var test in sortedTests)
        {
            Console.WriteLine($"{test.Test,-50} {test.AvgMs,6:F0}ms (max: {test.MaxMs}ms, runs: {test.Runs})");
        }
    }
}

// Register in fixture
public class PostgresCollectionFixture : IAsyncLifetime
{
    public async ValueTask DisposeAsync()
    {
        TestPerformanceMetrics.PrintSummary();
        // ...cleanup...
    }
}
```

### 5.3 CI Performance Metrics

**GitHub Actions**:
```yaml
- name: Run tests with performance tracking
  run: |
    dotnet test \
      --logger "trx;LogFileName=test-results.trx" \
      --logger "html;LogFileName=test-results.html"

- name: Extract test timings
  if: always()
  run: |
    # Parse TRX file for test timings
    dotnet tool install -g trx2junit
    trx2junit test-results.trx

    # Generate performance report
    python scripts/analyze-test-performance.py test-results.xml

- name: Upload performance report
  if: always()
  uses: actions/upload-artifact@v3
  with:
    name: test-performance-report
    path: test-performance-report.html
```

**Tracked Metrics**:
- Total test execution time (trend over commits)
- Per-test timing distribution
- Slowest tests (P95, P99)
- Test count by collection
- Parallelization efficiency

### 5.4 Performance Regression Prevention

**GitHub Actions Check**:
```yaml
- name: Check for performance regression
  run: |
    # Compare current run against baseline
    python scripts/check-performance-regression.py \
      --baseline baseline-timings.json \
      --current test-results.xml \
      --threshold 1.2  # Fail if >20% slower

    # Exit with error if regression detected
    if [ $? -ne 0 ]; then
      echo "❌ Performance regression detected"
      exit 1
    fi
```

---

## 6. Risk Assessment & Mitigation

### 6.1 High Risks

#### Risk 1: Intermittent Test Failures Due to Parallelization

**Probability**: High (70%)
**Impact**: High (CI instability, developer friction)

**Mitigation**:
1. **Incremental Rollout**:
   - Phase 1: Enable parallelization for 1 collection (20 tests)
   - Phase 2: Monitor for 1 week (3 CI runs/day)
   - Phase 3: Expand to 2 more collections if stable
   - Phase 4: Full rollout after 2 weeks stable

2. **Flakiness Detection**:
   ```bash
   # Run tests 10 times to detect flakiness
   for i in {1..10}; do
     dotnet test --filter "Collection=AdminUserMgmt" || echo "RUN $i FAILED"
   done
   ```

3. **Fast Rollback**:
   ```csharp
   // Feature flag for parallelization
   [CollectionDefinition("Admin User Management",
       DisableParallelization = Environment.GetEnvironmentVariable("DISABLE_TEST_PARALLELIZATION") == "true")]
   ```

4. **Detailed Logging**:
   ```csharp
   Console.WriteLine($"🧵 [Thread {Thread.CurrentThread.ManagedThreadId}] Starting {testName}");
   ```

#### Risk 2: Database Contention Under Parallel Load

**Probability**: Medium (50%)
**Impact**: Medium (slower tests, potential deadlocks)

**Mitigation**:
1. **Connection Pool Sizing**:
   ```csharp
   PostgreSqlBuilder()
       .WithImage("postgres:15-alpine")
       .WithCommand("postgres", "-c", "max_connections=200")  // Increased from default 100
   ```

2. **Deadlock Detection**:
   ```sql
   -- Log deadlocks in Postgres
   ALTER SYSTEM SET log_lock_waits = on;
   ALTER SYSTEM SET deadlock_timeout = '1s';
   ```

3. **Retry Logic for Deadlocks**:
   ```csharp
   protected async Task<T> WithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3)
   {
       for (int i = 0; i < maxRetries; i++)
       {
           try
           {
               return await action();
           }
           catch (NpgsqlException ex) when (ex.SqlState == "40P01" && i < maxRetries - 1)
           {
               // Deadlock detected, retry with exponential backoff
               await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, i) * 100));
           }
       }
       throw new Exception("Max retries exceeded");
   }
   ```

#### Risk 3: Shared Cache State Conflicts

**Probability**: High (80%)
**Impact**: Medium (test isolation violations)

**Mitigation**:
1. **Cache Namespace Isolation** (already designed in Section 1.2)
2. **Cache Flush Between Tests**:
   ```csharp
   public async Task InitializeAsync()
   {
       // Flush test cache namespace
       await CacheService.FlushNamespaceAsync($"test:{TestRunId}:*");
   }
   ```

3. **Cache Verification**:
   ```csharp
   [Fact]
   public async Task CacheIsolation_VerifyNoLeakage()
   {
       // Arrange: Test 1 sets cache
       var key1 = GetTestCacheKey("key");
       await CacheService.SetAsync(key1, "value1");

       // Act: Test 2 checks cache
       var key2 = GetTestCacheKey("key");  // Same key, different TestRunId
       var value2 = await CacheService.GetAsync(key2);

       // Assert: No cross-test contamination
       Assert.Null(value2);
   }
   ```

### 6.2 Medium Risks

#### Risk 4: DbContext Pool State Leakage

**Probability**: Low (30%)
**Impact**: High (test data corruption)

**Mitigation**:
1. **Aggressive State Clearing**:
   ```csharp
   public bool Return(MeepleAiDbContext obj)
   {
       obj.ChangeTracker.Clear();
       obj.Database.CloseConnection();  // Force connection reset
       return true;
   }
   ```

2. **Pool Validation**:
   ```csharp
   protected MeepleAiDbContext GetPooledDbContext()
   {
       var context = _dbContextPool.Get();

       // Verify clean state
       if (context.ChangeTracker.HasChanges())
       {
           throw new InvalidOperationException("DbContext returned from pool has unsaved changes");
       }

       return context;
   }
   ```

#### Risk 5: Container Reuse in CI

**Probability**: Medium (50%)
**Impact**: Low (test isolation in CI)

**Mitigation**:
- **Disable container reuse in CI**:
   ```csharp
   private bool ShouldReuseContainers() =>
       !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
   ```

### 6.3 Low Risks

#### Risk 6: Increased Memory Usage

**Probability**: Medium (60%)
**Impact**: Low (CI resource limits)

**Monitoring**:
```csharp
public async ValueTask DisposeAsync()
{
    var memoryMB = GC.GetTotalMemory(forceFullCollection: true) / 1024 / 1024;
    Console.WriteLine($"💾 [PostgresCollectionFixture] Memory usage: {memoryMB}MB");
}
```

**Mitigation**: Set GitHub Actions memory limits explicitly

---

## 7. Success Criteria & Benchmarks

### 7.1 Primary Success Criteria

| Metric | Baseline | Target | Measurement |
|--------|----------|--------|-------------|
| **Total Test Execution Time** | 15 minutes | 5-7 minutes | CI pipeline duration |
| **Individual Test Time** | Avg 6s | Avg 2-3s | Per-test timing logs |
| **Test Pass Rate** | 100% | 100% | No new failures introduced |
| **CI Stability** | 95% | ≥95% | 20 consecutive green runs |
| **Flakiness Rate** | <1% | <1% | Tests failing intermittently |

### 7.2 Performance Breakdown

**Phase 1: Parallelization** (Target: 40% reduction)
- Baseline: 900s sequential
- Target: 540s with 4 parallel threads
- Measurement: Total CI time

**Phase 2: Database Optimization** (Target: 20% reduction)
- Baseline: 150ms cleanup + 150ms user creation per test
- Target: 15ms cleanup + 0ms shared users (66% of tests)
- Measurement: Per-test timing logs

**Phase 3: Fixture Optimization** (Target: 10% reduction)
- Baseline: 3-5s container startup per run
- Target: 0.5s container reuse (local) / 10s image pre-pull savings (CI)
- Measurement: Fixture initialization logs

### 7.3 Quality Gates

**Before merging each phase**:
1. ✅ All tests pass 10 consecutive times
2. ✅ No new warnings or errors in logs
3. ✅ Code review approved by 2 team members
4. ✅ Performance improvement documented (before/after metrics)
5. ✅ No increase in memory usage (>10% threshold)

---

## 8. Implementation Roadmap

### Week 1: Parallelization Foundation

**Days 1-2**: Split AdminTestCollection
- Create 5 new collection definitions
- Migrate test files to new collections
- Run 10x verification loop

**Days 3-4**: Add Cache Isolation
- Implement GetTestCacheKey helper
- Update CacheAdminEndpointsTests
- Add cache isolation verification tests

**Day 5**: Add Configuration Isolation
- Implement GetTestConfigKey helper
- Update configuration tests
- Document isolation patterns

**Deliverable**: Parallelization enabled, 40% time reduction achieved

### Week 2: Database Optimization

**Days 1-2**: Batch Cleanup Implementation
- Implement single SQL script cleanup
- Test with high concurrency (20 parallel tests)
- Measure cleanup time improvement

**Days 3-4**: Shared Test Users
- Add shared users to PostgresCollectionFixture
- Migrate read-only tests to use shared users
- Document when to use shared vs unique users

**Day 5**: Database Seeding
- Pre-seed common games (Chess, Tic-Tac-Toe)
- Update tests to use fixture games where appropriate
- Verify no state leakage

**Deliverable**: Database operations optimized, additional 20% time reduction

### Week 3: Fixture & CI Optimization

**Days 1-2**: DbContext Pooling
- Implement DbContextPooledObjectPolicy
- Add WithDbContextAsync helper
- Test pool behavior under load

**Days 3-4**: Container Optimization
- Implement container reuse (local dev)
- Add CI image pre-pull
- Test parallel container initialization

**Day 5**: CI Configuration
- Update GitHub Actions workflow
- Add test sharding (4 jobs)
- Configure timeout optimizations

**Deliverable**: Fixture overhead reduced, CI infrastructure optimized

### Week 4: Monitoring & Validation

**Days 1-2**: Performance Instrumentation
- Add TestTimingScope
- Implement TestPerformanceMetrics
- Configure CI performance tracking

**Days 3-4**: Regression Prevention
- Create baseline-timings.json
- Add performance regression check to CI
- Document performance monitoring process

**Day 5**: Final Validation & Documentation
- Run full test suite 20 times (CI + local)
- Document all optimizations
- Create rollback procedures
- Team presentation & knowledge transfer

**Deliverable**: Complete optimization suite, monitoring in place

---

## 9. Rollback Procedures

### 9.1 Emergency Rollback (CI Broken)

**Symptoms**:
- Test failures >5% in CI
- Tests hanging or timing out
- Intermittent deadlocks

**Immediate Action** (within 15 minutes):
```bash
# Revert parallelization
git revert <commit-hash-of-parallelization>

# Or: Emergency feature flag
export DISABLE_TEST_PARALLELIZATION=true
dotnet test
```

**Communication**:
- Post in team Slack: "CI tests unstable, parallelization reverted temporarily"
- Create incident ticket with logs

### 9.2 Partial Rollback (Specific Collection)

**Symptoms**:
- One collection has intermittent failures
- Other collections are stable

**Action**:
```csharp
// Re-enable serialization for problematic collection only
[CollectionDefinition("Admin Cache Management", DisableParallelization = true)]
public class AdminCacheMgmtCollection : ICollectionFixture<PostgresCollectionFixture>
```

### 9.3 Phased Rollback

**Strategy**: Roll back optimizations in reverse order of implementation

1. **Week 4**: Disable performance monitoring (logs only, no behavioral changes)
2. **Week 3**: Remove container reuse, DbContext pooling
3. **Week 2**: Remove shared users, revert to individual cleanup
4. **Week 1**: Re-enable DisableParallelization, merge collections

**Decision Criteria**:
- Performance gain < 20% of target → Rollback Week 3
- Test instability → Rollback Week 1
- Memory issues → Rollback Week 2-3

---

## 10. Appendix

### 10.1 xUnit v3 Best Practices

**Collection Fixtures**:
- Shared across all tests in collection
- Created once per collection
- Ideal for expensive setup (containers, databases)

**Class Fixtures**:
- Shared across tests in a single class
- Created once per test class
- Ideal for per-class configuration

**Parallelization Defaults** (xUnit v3):
- Test classes run in parallel by default
- Tests within a class run serially by default
- Collections run in parallel by default
- `DisableParallelization = true` forces collection serialization

### 10.2 Testcontainers Performance Tips

1. **Use Alpine Images**: `postgres:15-alpine` is 75% smaller than `postgres:15`
2. **WithWaitStrategy**: Explicit wait prevents premature queries
3. **WithReuse**: Local dev optimization (containers persist between runs)
4. **Parallel Container Start**: `Task.WhenAll()` for multiple containers
5. **Resource Limits**: Set `--memory=512m --cpus=1` for faster startup

### 10.3 PostgreSQL Connection Pooling

**Npgsql Connection String**:
```
Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=testpassword;
Pooling=true;MinPoolSize=2;MaxPoolSize=50;ConnectionIdleLifetime=300;
```

**Key Parameters**:
- `MinPoolSize=2`: Keep 2 warm connections
- `MaxPoolSize=50`: Support up to 50 concurrent tests
- `ConnectionIdleLifetime=300`: Close idle connections after 5 minutes

### 10.4 PBKDF2 Hashing Performance

**Current Implementation** (from AuthService):
```csharp
const int iterations = 210_000;  // OWASP recommendation for SHA256
var salt = RandomNumberGenerator.GetBytes(16);
var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
```

**Timing**:
- 210K iterations: ~150ms on typical CI runner (2 CPU cores)
- 100K iterations: ~70ms (not recommended, below OWASP minimum)

**Test Optimization** (NOT for production):
```csharp
#if DEBUG
const int iterations = 10_000;  // Fast hashing for tests only
#else
const int iterations = 210_000;  // Secure hashing for production
#endif
```

**Security Consideration**: Only use reduced iterations in test environments with DEBUG flag, never in production code paths.

---

## 11. References

- [xUnit v3 Documentation](https://xunit.net/docs/)
- [Testcontainers .NET](https://dotnet.testcontainers.org/)
- [GitHub Actions Best Practices](https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions)
- [PostgreSQL Connection Pooling](https://www.npgsql.org/doc/connection-string-parameters.html)
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)

---

## Change Log

| Date | Version | Author | Changes |
|------|---------|--------|---------|
| 2025-11-10 | 1.0 | System Architect | Initial design document |

---

**Next Steps**:
1. Review with team (30-minute meeting)
2. Approval from Tech Lead
3. Begin Week 1 implementation
4. Daily stand-up updates on progress
