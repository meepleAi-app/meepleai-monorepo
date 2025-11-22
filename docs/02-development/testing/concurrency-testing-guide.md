# Concurrency Testing Guide

## Overview

This guide documents comprehensive concurrency testing patterns implemented for the MeepleAI API to detect and prevent race conditions across critical services.

**Issue**: #601
**Branch**: test-601-concurrency-tests
**Reference Implementation**: ConfigurationConcurrencyTests.cs

## Test Patterns

### Pattern 1: Concurrent Writes (Lost Update Detection)

**Purpose**: Detect lost updates when multiple operations write to the same resource simultaneously.

**Pattern**:
```csharp
[Fact]
public async Task ConcurrentWrites_NoLostUpdates()
{
    // Arrange: Create shared resource
    var resourceId = await CreateTestResource();

    // Act: 10-30 concurrent write operations
    var tasks = Enumerable.Range(1, 30)
        .Select(i => Task.Run(() => UpdateResource(resourceId, i)))
        .ToArray();
    await Task.WhenAll(tasks);

    // Assert: Verify all updates accounted for (no lost updates)
    var finalState = await GetResourceState(resourceId);
    Assert.NotNull(finalState);
    Assert.True(finalState.IsConsistent);
}
```

**What to Test**:
- Multiple writes to same database row
- File system operations on same file
- Cache updates to same key
- Configuration changes

**Expected Behavior**:
- Last write wins (acceptable for most cases)
- OR Optimistic concurrency exception (preferred)
- NO silent data loss

### Pattern 2: Optimistic Concurrency (Read-Modify-Write)

**Purpose**: Verify optimistic concurrency control detects conflicting updates.

**Pattern**:
```csharp
[Fact]
public async Task ReadModifyWrite_OptimisticConcurrency_DetectsConflict()
{
    // Arrange: Create resource
    var resourceId = await CreateTestResource();

    var conflictCount = 0;
    var successCount = 0;

    // Act: Concurrent read-modify-write operations
    var tasks = Enumerable.Range(1, 30).Select(async i =>
    {
        try
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            // Read
            var entity = await dbContext.Entities.FirstAsync(e => e.Id == resourceId);

            // Simulate processing delay
            await Task.Delay(10);

            // Modify
            entity.Value = $"Update {i}";
            entity.UpdatedAt = DateTime.UtcNow;

            // Write
            await dbContext.SaveChangesAsync();
            Interlocked.Increment(ref successCount);
        }
        catch (DbUpdateConcurrencyException)
        {
            Interlocked.Increment(ref conflictCount);
        }
    }).ToArray();

    await Task.WhenAll(tasks);

    // Assert: Some conflicts detected (proves concurrency control works)
    Assert.True(conflictCount > 0, "Should detect concurrent modification conflicts");
    Assert.True(successCount > 0, "Some updates should succeed");
}
```

**What to Test**:
- EF Core entities without proper concurrency tokens
- Version-based updates
- Status transitions
- Financial transactions

**Expected Behavior**:
- DbUpdateConcurrencyException thrown for conflicts
- Automatic retry logic (if implemented)
- Consistent final state

### Pattern 3: TOCTOU (Time-Of-Check-Time-Of-Use)

**Purpose**: Detect race conditions between existence checks and subsequent operations.

**Pattern**:
```csharp
[Fact]
public async Task CheckThenUse_TOCTOU_GracefulHandling()
{
    // Arrange: Create resource
    var resourceId = await CreateTestResource();

    // Act: Concurrent check-then-use operations
    var tasks = Enumerable.Range(1, 15).Select(async i =>
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<Service>();

        // Time of check
        var exists = await service.ResourceExistsAsync(resourceId);

        if (exists)
        {
            // Time of use (resource might be deleted here)
            try
            {
                await service.UseResourceAsync(resourceId);
                return "success";
            }
            catch (InvalidOperationException)
            {
                return "not-found";
            }
        }

        return "did-not-exist";
    }).ToArray();

    var results = await Task.WhenAll(tasks);

    // Assert: TOCTOU handled gracefully (no crashes)
    Assert.All(results, result => Assert.NotEqual("crashed", result));
}
```

**What to Test**:
- File existence checks before operations
- Database row checks before updates
- Permission checks before actions
- Resource availability checks

**Expected Behavior**:
- Graceful exception handling
- Proper error messages
- No data corruption
- Idempotent operations preferred

### Pattern 4: Cache Coherence

**Purpose**: Verify cache invalidation propagates correctly under concurrent load.

**Pattern**:
```csharp
[Fact]
public async Task CacheInvalidation_Concurrent_NoStaleData()
{
    // Arrange: Populate cache
    var key = "test-key";
    await cache.SetAsync(key, "initial-value");

    // Act: Concurrent updates and cache invalidations
    var updateTask = Task.Run(async () =>
    {
        for (int i = 0; i < 5; i++)
        {
            await UpdateResource(key, $"value-{i}");
            await cache.InvalidateAsync(key);
            await Task.Delay(20);
        }
    });

    var readTasks = Enumerable.Range(0, 20).Select(async i =>
    {
        await Task.Delay(i * 5);
        var value = await cache.GetOrCreateAsync(key, () => GetFromDatabase(key));
        return value;
    }).ToArray();

    await Task.WhenAll(readTasks.Concat(new[] { updateTask }));

    // Assert: No stale data after updates complete
    var finalValue = await cache.GetAsync(key);
    var dbValue = await GetFromDatabase(key);
    Assert.Equal(dbValue, finalValue);
}
```

**What to Test**:
- Redis cache invalidation
- In-memory cache coherence
- Distributed cache consistency
- Cache-aside pattern

**Expected Behavior**:
- Cache eventually consistent with database
- No permanent stale data
- Proper TTL handling
- Race-free invalidation

## Test Coverage by Service

### ✅ ConfigurationService (Reference Implementation)
- ✅ Concurrent edits of different configs
- ✅ Optimistic concurrency on same config
- ✅ Read consistency during writes
- ✅ Cache invalidation propagation
- ✅ Distributed cache coherence

**File**: `ConfigurationConcurrencyTests.cs` (5 tests)

### ✅ RuleSpecService (HIGH PRIORITY)
- ✅ Concurrent version creation
- ✅ Concurrent draft updates (optimistic concurrency)
- ✅ Read consistency during writes
- ✅ GetOrCreateDemo TOCTOU scenario

**File**: `RuleSpecConcurrencyTests.cs` (4 tests)

**Known Issues**:
- GetOrCreateDemo may create 1-2 duplicate demos under high concurrency (acceptable)
- Version updates have last-write-wins behavior (consider adding version tracking)

### ✅ ChatService (HIGH PRIORITY)
- ✅ Concurrent message creation in same chat
- ✅ Concurrent message deletion consistency
- ✅ Read consistency during message creation
- ✅ Concurrent chat creation for same user/game

**File**: `ChatServiceConcurrencyTests.cs` (4 tests)

**Known Issues**:
- No issues detected during testing
- Message ordering relies on CreatedAt timestamps (race condition possible but unlikely)

### ✅ PdfStorageService (HIGH PRIORITY)
- ✅ Concurrent uploads for same game
- ✅ Upload during processing (no corruption)
- ✅ Concurrent delete operations (no orphaned data)
- ✅ Get-then-delete TOCTOU scenario

**File**: `PdfStorageConcurrencyTests.cs` (4 tests)

**Known Issues**:
- TOCTOU timing window exists but handled gracefully with exceptions
- Consider implementing optimistic locking for PDF metadata updates

### ✅ UserManagementService (MEDIUM PRIORITY)
- ✅ Concurrent user creation with same email
- ✅ Concurrent user updates (optimistic concurrency)
- ✅ Concurrent role assignments
- ✅ Concurrent delete and read (no orphaned sessions)

**File**: `UserManagementConcurrencyTests.cs` (4 tests)

**Known Issues**:
- No issues detected during testing
- Proper unique constraints and foreign key cascades protect data integrity

### ✅ PromptTemplateService (MEDIUM PRIORITY)
- ✅ Concurrent version activation (only one active)
- ✅ Concurrent cache invalidation
- ✅ Read consistency during version activation
- ✅ Cache invalidation during activation

**File**: `PromptTemplateConcurrencyTests.cs` (4 tests)

**Known Issues**:
- Cache timing can cause brief inconsistencies (acceptable with 1-hour TTL)
- Consider implementing distributed locks for version activation

### ✅ N8nConfigService (MEDIUM PRIORITY)
- ✅ Concurrent config updates (optimistic concurrency)
- ✅ Concurrent enable/disable toggles
- ✅ Read consistency during updates

**File**: `N8nConfigConcurrencyTests.cs` (3 tests)

**Known Issues**:
- No issues detected during testing
- Simple boolean state transitions are atomic

## Running Concurrency Tests

### Run All Concurrency Tests
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~ConcurrencyTests"
```

### Run Specific Service Tests
```bash
# RuleSpec concurrency tests
dotnet test --filter "FullyQualifiedName~RuleSpecConcurrencyTests"

# Chat concurrency tests
dotnet test --filter "FullyQualifiedName~ChatServiceConcurrencyTests"

# PDF concurrency tests
dotnet test --filter "FullyQualifiedName~PdfStorageConcurrencyTests"

# User management concurrency tests
dotnet test --filter "FullyQualifiedName~UserManagementConcurrencyTests"

# Prompt template concurrency tests
dotnet test --filter "FullyQualifiedName~PromptTemplateConcurrencyTests"

# N8N config concurrency tests
dotnet test --filter "FullyQualifiedName~N8nConfigConcurrencyTests"
```

### Run Single Test
```bash
dotnet test --filter "FullyQualifiedName~RuleSpecConcurrencyTests.ConcurrentVersionCreation_OnSameRuleSpec_NoLostUpdates"
```

### Run with Verbose Output
```bash
dotnet test --filter "FullyQualifiedName~ConcurrencyTests" --logger "console;verbosity=detailed"
```

## Interpreting Test Results

### ✅ Success Indicators
- All tests pass on first run
- No unhandled exceptions
- Database state is consistent after concurrent operations
- No data corruption or lost updates
- Graceful handling of race conditions

### ⚠️ Warning Signs
- Intermittent test failures (flaky tests)
- DbUpdateConcurrencyException count = 0 (concurrency control not working)
- Data inconsistencies after concurrent operations
- Deadlocks or timeouts
- Orphaned data or broken references

### ❌ Critical Issues
- Test crashes or hangs
- Data corruption
- Silent data loss (no exceptions but wrong results)
- Cascade delete failures
- Foreign key constraint violations

## Known Race Conditions Catalog

### 1. RuleSpecService.GetOrCreateDemo (LOW SEVERITY)
**Symptom**: Multiple demo rule specs created instead of one
**Impact**: Minor - Extra demo entries (self-cleaning)
**Mitigation**: Add unique constraint or distributed lock
**Workaround**: Acceptable behavior for demo data
**Status**: Documented, no fix planned

### 2. PromptTemplateService Cache Timing (LOW SEVERITY)
**Symptom**: Brief inconsistency between cache and database after version activation
**Impact**: Minimal - Resolves within cache TTL (1 hour)
**Mitigation**: Reduce cache TTL or implement distributed locks
**Workaround**: Acceptable with current TTL
**Status**: Documented, monitoring

## Future Expansion Plans

### Phase 2: Additional Services
- [ ] SessionManagementService (session cleanup races)
- [ ] AuthService (concurrent login/logout)
- [ ] QdrantService (concurrent vector operations)
- [ ] EmbeddingService (concurrent embedding generation)
- [ ] BackgroundTaskService (task queue races)

### Phase 3: Advanced Patterns
- [ ] Distributed transaction tests
- [ ] Cross-service consistency tests
- [ ] Event sourcing race conditions
- [ ] Message queue ordering tests
- [ ] WebSocket concurrent connection tests

### Phase 4: Performance Testing
- [ ] Load testing under concurrency
- [ ] Deadlock detection tests
- [ ] Connection pool exhaustion tests
- [ ] Redis connection limit tests
- [ ] Database connection pooling stress tests

## Best Practices

### Writing Concurrency Tests

1. **Use Real Database**: Always use Testcontainers, not in-memory databases
2. **High Concurrency**: Test with 10-30 concurrent operations minimum
3. **Stagger Operations**: Use `Task.Delay(i * 5)` to vary timing
4. **Verify Final State**: Always check database consistency after tests
5. **Log Results**: Use ITestOutputHelper for debugging
6. **Handle Exceptions**: Catch expected exceptions, count them, assert behavior
7. **Test TOCTOU**: Explicitly test check-then-use patterns
8. **Cache Testing**: Test cache invalidation under concurrent load

### Avoiding Flaky Tests

1. **No Sleep-Based Timing**: Use proper synchronization, not arbitrary delays
2. **Idempotent Assertions**: Assert on final state, not intermediate
3. **Allow Variance**: Use ranges (e.g., `>= 1`, not `== 15`) for concurrent results
4. **Weak Assertions for Timing**: Don't assert exact timing-dependent behavior
5. **Strong Assertions for Correctness**: Always assert data consistency
6. **Repeat Tests**: Run 10x in CI to catch intermittent issues
7. **Isolation**: Each test creates its own data, no shared state

### Production Considerations

1. **Optimistic Concurrency**: Add RowVersion/ConcurrencyToken to critical entities
2. **Distributed Locks**: Use Redis locks for critical sections
3. **Idempotent APIs**: Design APIs to handle duplicate requests safely
4. **Transaction Isolation**: Use appropriate isolation levels (ReadCommitted, Serializable)
5. **Retry Logic**: Implement exponential backoff for transient failures
6. **Circuit Breakers**: Prevent cascade failures under contention
7. **Monitoring**: Track concurrency metrics (conflicts, retries, deadlocks)

## Troubleshooting

### Tests Hang or Timeout
**Cause**: Deadlock or resource contention
**Solution**: Check for circular dependencies, reduce concurrency level, add timeout

### All Tests Pass but Production Has Issues
**Cause**: Test concurrency too low or timing too artificial
**Solution**: Increase concurrent operations (50-100), add random delays

### Flaky Tests (Pass/Fail Randomly)
**Cause**: Race condition in test itself or timing assumptions
**Solution**: Remove sleep-based timing, use proper synchronization, add logging

### DbUpdateConcurrencyException Count = 0
**Cause**: Concurrency control not working (missing RowVersion)
**Solution**: Add ConcurrencyToken attribute to entity, verify EF configuration

### Data Inconsistencies After Tests
**Cause**: Real race condition in service code
**Solution**: Add optimistic locking, transactions, or distributed locks

## References

- **EF Core Concurrency**: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- **TOCTOU Wikipedia**: https://en.wikipedia.org/wiki/Time-of-check_to_time-of-use
- **xUnit Parallel Testing**: https://xunit.net/docs/running-tests-in-parallel
- **Testcontainers**: https://dotnet.testcontainers.org/

## Summary

**Total Test Files**: 6 new concurrency test files
**Total Tests**: 23 tests (4+4+4+4+4+3)
**Coverage**: 6 critical services (ConfigurationService already covered)
**Patterns**: 4 core concurrency patterns implemented
**Severity Levels**: High (3 services), Medium (3 services)

All tests follow the established ConfigurationConcurrencyTests.cs pattern, use real databases via Testcontainers, and provide comprehensive race condition detection across the MeepleAI API codebase.
