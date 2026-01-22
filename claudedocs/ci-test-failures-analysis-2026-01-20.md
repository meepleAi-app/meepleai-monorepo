# CI Test Failures Analysis Report

**Date**: 2026-01-20
**Branch**: main-dev
**Analyst**: Claude Code (Opus 4.5)

## Executive Summary

Analysis of CI test failures revealed **7 distinct failure categories** with root causes ranging from environment variable leakage to test infrastructure timing issues. Priority fixes are ranked by impact and complexity.

---

## 1. SecretsHelper Tests - Environment Variable Isolation

### Status: ✅ FIXED LOCALLY

### Failing Tests
- `BuildPostgresConnectionString_UsesDefaults`
- `BuildPostgresConnectionString_Success`

### Root Cause
CI environment sets `POSTGRES_HOST=localhost`, `POSTGRES_DB=meepleai_test` which leaked into tests. The `SecretsHelper.BuildPostgresConnectionString()` method checks `Environment.GetEnvironmentVariable()` **before** `IConfiguration`, causing CI variables to override test configuration.

### Fix Applied
Added environment variable save/restore pattern in tests:
```csharp
// Save original values
var originalHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
try
{
    Environment.SetEnvironmentVariable("POSTGRES_HOST", null); // Clear for test
    // ... test logic ...
}
finally
{
    Environment.SetEnvironmentVariable("POSTGRES_HOST", originalHost); // Restore
}
```

Also corrected expected default from `Host=postgres` to `Host=localhost` (the actual default in `SecretsHelper.cs:125`).

### Priority: 🔴 **CRITICAL** - Blocks CI, already fixed
### Type: Test Issue

---

## 2. Rate Limiting (429 TooManyRequests) Failures

### Status: ⚠️ NEEDS FIX

### Failing Tests (~20 tests)
- `AuthenticationFlowTests.*`
- `EdgeCaseTests.*`
- `ErrorHandlingTests.*`

### Root Cause Analysis
Tests use `FrontendSdkTestFactory` which creates a real `WebApplicationFactory<Program>`. The production rate limiting middleware is **NOT disabled** in test configuration.

**Evidence from `FrontendSdkTestFactory.cs:121-137`**:
- Configuration includes retry/circuit breaker settings but **NO rate limiting disable flag**
- All tests in `FrontendSdkTestCollection` share the same factory instance
- Rapid test execution triggers rate limiting thresholds

### Recommended Fix
Add rate limiting configuration override in `FrontendSdkTestFactory.ConfigureAppConfiguration()`:

```csharp
configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
{
    // Existing config...

    // Disable rate limiting for tests
    ["RateLimiting:Enabled"] = "false",
    // OR increase limits significantly
    ["RateLimiting:MaxRequestsPerMinute"] = "10000",
    ["RateLimiting:WindowSizeSeconds"] = "1",
});
```

**Alternative**: Add test middleware that bypasses rate limiting when `X-Test-Bypass-RateLimit` header is present.

### Priority: 🔴 **CRITICAL** - Affects 20+ tests
### Type: Test Infrastructure Issue

---

## 3. PostgreSQL Connection Termination (57P01)

### Status: ⚠️ NEEDS FIX

### Failing Tests
- `UploadPdfIntegrationTests.*`
- `UploadPdfMidPhaseCancellationTests.*`

### Error
```
Npgsql.PostgresException: 57P01: terminating connection due to administrator command
```

### Root Cause Analysis
The `SharedTestcontainersFixture.DropIsolatedDatabaseAsync()` method at lines 354-361 terminates all connections to a database before dropping it:

```sql
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = '{databaseName}' AND pid <> pg_backend_pid();
```

**Problem**: If a test is still executing when another test's cleanup runs, active connections get terminated. This is a **test isolation issue** caused by:
1. Parallel test execution in CI
2. Tests sharing the `SharedTestcontainersFixture`
3. Long-running PDF upload operations

### Recommended Fix
1. **Increase connection timeout resilience** in connection string:
```csharp
"Timeout=60;CommandTimeout=120;Keepalive=30;"
```

2. **Add retry logic** for connection-sensitive operations in tests:
```csharp
await RetryPolicy.ExecuteAsync(async () =>
{
    await _dbContext.SaveChangesAsync(ct);
});
```

3. **Consider sequential execution** for PDF tests:
```csharp
[Collection("SequentialPdfTests")] // Separate collection
```

### Priority: 🟡 **HIGH** - Intermittent but blocks CI
### Type: Test Infrastructure Issue

---

## 4. Disposed DbContext Failures

### Status: ⚠️ NEEDS FIX

### Failing Tests
- `SharedGameCatalogEndpointsIntegrationTests.SubmitForApproval_*`
- `SharedGameCatalogEndpointsIntegrationTests.ApprovePublication_*`
- `SharedGameCatalogEndpointsIntegrationTests.RejectPublication_*`
- `SharedGameCatalogEndpointsIntegrationTests.GetPendingApprovals_*`

### Error
```
System.ObjectDisposedException: Cannot access a disposed context instance.
```

### Root Cause Analysis
From the test file structure, the test stores `_dbContext` from a `ServiceScope` that gets disposed before assertions. Pattern observed:

```csharp
// PROBLEMATIC: Storing context from scoped service
var scope = _factory.Services.CreateScope();
_dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
// ... test logic ...
scope.Dispose(); // Context now disposed!
// ... assertions using _dbContext FAIL
```

### Recommended Fix
Use `ChangeTracker.Clear()` pattern instead of storing scoped DbContext:

```csharp
[Fact]
public async Task Test_ShouldWork()
{
    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    // ... operations ...

    // Clear tracker before assertions (prevents tracking issues)
    dbContext.ChangeTracker.Clear();

    // ... assertions ...
} // scope disposes cleanly here
```

Or restructure to create fresh scope for assertions:

```csharp
// Act
using (var actScope = _factory.Services.CreateScope())
{
    var actContext = actScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    // ... operations ...
}

// Assert with fresh scope
using (var assertScope = _factory.Services.CreateScope())
{
    var assertContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var result = await assertContext.Games.FindAsync(gameId);
    result.Should().NotBeNull();
}
```

### Priority: 🟡 **HIGH** - Affects multiple tests
### Type: Test Issue

---

## 5. FK Constraint Test Failures

### Status: ⚠️ NEEDS FIX

### Failing Tests
- `DocumentCollectionUserRestrictionTests.*`
- `SystemConfigurationForeignKeyConstraintsTests.*`

### Error
```
Expected DbUpdateException but got InvalidOperationException
```

### Root Cause Analysis
Tests expect `DbUpdateException` when FK constraint violations occur, but EF Core throws `InvalidOperationException` in certain scenarios:

1. **Scenario 1**: Entity not tracked - When trying to delete a parent that has untracked children, EF throws `InvalidOperationException` instead of allowing the database to throw FK violation.

2. **Scenario 2**: Delete behavior cascade - If `DeleteBehavior.Restrict` is configured but EF validates relationships in memory before database call.

### Recommended Fix
1. **Ensure entities are properly seeded** before testing FK constraints:
```csharp
// Seed parent
_dbContext.Users.Add(user);
await _dbContext.SaveChangesAsync();
_dbContext.ChangeTracker.Clear();

// Seed dependent (loads fresh)
var freshUser = await _dbContext.Users.FindAsync(userId);
_dbContext.DocumentCollections.Add(new DocumentCollection { UserId = userId });
await _dbContext.SaveChangesAsync();
```

2. **Catch broader exception type** and verify message:
```csharp
// Instead of:
await Assert.ThrowsAsync<DbUpdateException>(...);

// Use:
var ex = await Assert.ThrowsAnyAsync<Exception>(...);
Assert.True(ex is DbUpdateException or InvalidOperationException);
Assert.Contains("foreign key", ex.Message, StringComparison.OrdinalIgnoreCase);
```

### Priority: 🟡 **HIGH** - Test correctness issue
### Type: Test Issue

---

## 6. Concurrency Test Failures

### Status: ⚠️ NEEDS FIX

### Failing Tests
- `TransactionScenarioTests.ConcurrentTransactions_PotentialDeadlock_ShouldHandleGracefully`
- `TransactionScenarioTests.ConcurrentUpdate_OptimisticLocking_ShouldThrowDbUpdateConcurrencyException`

### Root Cause Analysis

**Test 1: Deadlock Test** (`lines 252-339`)
The test expects both concurrent transactions to succeed with retry logic:
```csharp
successCount.Should().Be(2, "Both transactions should succeed with retry logic");
```

**Problem**: The retry logic only catches `DbUpdateException`, but PostgreSQL deadlocks throw `NpgsqlException` with code `40P01`:
```csharp
catch (DbUpdateException) when (retry < 2) // Misses NpgsqlException!
```

**Test 2: Optimistic Locking** (`lines 199-246`)
The test expects `DbUpdateConcurrencyException` when two contexts update the same entity.

**Problem**: `GameEntity` may not have a `RowVersion` property configured for optimistic concurrency. Without `[Timestamp]` attribute, EF Core won't detect the conflict.

### Recommended Fix

**For Deadlock Test**:
```csharp
catch (Exception ex) when (retry < 2 &&
    (ex is DbUpdateException ||
     (ex is NpgsqlException npgsqlEx && npgsqlEx.SqlState == "40P01")))
```

**For Optimistic Locking Test**:
Verify `GameEntity` has concurrency token:
```csharp
public class GameEntity
{
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}
```

Or skip test if optimistic locking not configured:
```csharp
[Fact(Skip = "Requires RowVersion concurrency token on GameEntity")]
```

### Priority: 🟡 **MEDIUM** - Edge case tests
### Type: Test Issue (may indicate missing production feature)

---

## 7. Other Failures (Moq, Redis, HybridCache, etc.)

### 7.1 WeeklyEvaluationServiceTests - Moq DateTime Comparison

**Test**: `ExecuteAsync_GeneratesQualityReport_Successfully`

**Error**: Moq verification failure - DateTime comparison doesn't match.

**Root Cause**: Test uses hardcoded dates but `FakeTimeProvider.Advance()` changes the time between setup and verification:
```csharp
// Setup expects:
q.StartDate.Date == new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc).Date
// But FakeTimeProvider has advanced by InitialDelayMinutes + 0.001
```

**Fix**: Use date-only comparison or capture actual query in callback (already done at line 192-197, just needs better assertions).

**Priority**: 🟢 **LOW** - Unit test timing

---

### 7.2 RedisBackgroundTaskOrchestratorTests - Assert.True Failure

**Test**: `ScheduleAsync_ValidTask_SchedulesSuccessfully`

**Error**: `Assert.True(taskExecuted)` fails.

**Root Cause**: Race condition between scheduling and assertion. The `SmallDelay` (from `TestConstants.Timing`) may not be sufficient for the task to complete:
```csharp
await Task.Delay(TestConstants.Timing.SmallDelay, TestCancellationToken);
Assert.True(taskExecuted); // May fail if SmallDelay < task execution time
```

**Fix**: Increase delay or use `TaskCompletionSource` for reliable synchronization:
```csharp
var tcs = new TaskCompletionSource<bool>();
Func<CancellationToken, Task> taskFactory = async ct =>
{
    await Task.Delay(TestConstants.Timing.TinyDelay, ct);
    tcs.SetResult(true);
};

await _orchestrator.ScheduleAsync(...);
var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
Assert.True(tcs.Task.IsCompletedSuccessfully);
```

**Priority**: 🟢 **LOW** - Test timing

---

### 7.3 HybridCacheServiceIntegrationTests - Redis L2 Not Storing

**Test**: `GetOrCreateAsync_WithFullMiss_ExecutesFactoryAndPopulatesBothLevels`

**Error**: `redisExists.Should().BeTrue("value should be stored in Redis L2")` fails.

**Root Cause**: The test checks wrong Redis key. The `HybridCache` uses internal key format, not `_keyPrefix + key`:
```csharp
var redisKey = _keyPrefix + key.Replace(_keyPrefix, "");  // Double prefix issue
var redisExists = await redisDb.KeyExistsAsync(redisKey);
```

**Fix**: Use Redis SCAN to find actual key or verify via HybridCache API instead of direct Redis:
```csharp
// Better approach: verify via cache API
var result2 = await _cacheService.GetOrCreateAsync(key,
    _ => Task.FromResult(new TestCacheData { Value = "should_not_call" }));
result2.Value.Should().Be("new_value"); // Proves L2 cache hit
```

**Priority**: 🟢 **LOW** - Test verification method

---

### 7.4 LoginWithApiKeyCommandHandlerIntegrationTests - NpgsqlOperationInProgressException

**Test**: `Handle_WithValidApiKey_ReturnsUserProfile`

**Error**: `NpgsqlOperationInProgressException`

**Root Cause**: Multiple concurrent operations on single connection. The test may have:
1. Pending transaction from previous test
2. Async enumerable not fully consumed

**Fix**: Ensure clean state:
```csharp
public async ValueTask InitializeAsync()
{
    // ... existing setup ...

    // Clear any pending operations
    await using var cmd = new NpgsqlConnection(_isolatedDbConnectionString).CreateCommand();
    cmd.CommandText = "SELECT 1;"; // Simple query to ensure connection is clean
}
```

**Priority**: 🟢 **LOW** - Intermittent

---

### 7.5 DomainEventDispatcherIntegrationTests - Null Reference

**Test**: `MultipleContextEvents_ShouldAllDispatchInSameTransaction`

**Error**: `NullReferenceException`

**Root Cause**: At line 270, `ApiKey.Create()` returns a tuple but test only uses first element. The `_eventCollector.CollectEventsFrom(apiKey)` may fail if `apiKey` is null or doesn't implement `IHasDomainEvents`:
```csharp
var apiKey = ApiKey.Create(Guid.NewGuid(), user.Id, "Test API Key", "read,write").Item1;
_eventCollector.CollectEventsFrom(apiKey); // May be null
```

**Fix**: Add null check or ensure `ApiKey` type implements required interface:
```csharp
var (apiKey, _) = ApiKey.Create(...);
if (apiKey is IHasDomainEvents eventSource)
{
    _eventCollector.CollectEventsFrom(eventSource);
}
```

**Priority**: 🟢 **LOW** - Test error handling

---

## Summary Table

| Category | Tests Affected | Priority | Type | Status |
|----------|---------------|----------|------|--------|
| SecretsHelper | 2 | 🔴 Critical | Test | ✅ Fixed |
| Rate Limiting (429) | ~20 | 🔴 Critical | Test Infra | ⚠️ Needs Fix |
| PostgreSQL 57P01 | ~5 | 🟡 High | Test Infra | ⚠️ Needs Fix |
| Disposed DbContext | 4 | 🟡 High | Test | ⚠️ Needs Fix |
| FK Constraints | ~4 | 🟡 High | Test | ⚠️ Needs Fix |
| Concurrency | 2 | 🟡 Medium | Test/Prod | ⚠️ Needs Fix |
| Timing/Moq | ~5 | 🟢 Low | Test | ⚠️ Needs Fix |

## Recommended Fix Order

1. **Commit SecretsHelper fix** (already done locally)
2. **Disable rate limiting in FrontendSdkTestFactory** - Unblocks 20+ tests
3. **Fix DbContext scope management** - Pattern fix applies to multiple tests
4. **Add retry logic for 57P01** - Infrastructure resilience
5. **Update FK constraint test expectations** - Test correctness
6. **Fix concurrency tests** - Add proper exception handling
7. **Address timing issues** - Low priority, test reliability

---

*Report generated by Claude Code analysis of CI test failures.*
