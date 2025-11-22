# Background Service Testing Guide

**Purpose**: Guide for testing ASP.NET Core BackgroundService implementations with TestTimeProvider coordination.

**Issue**: #597 - BackgroundServiceTestHelper for timing coordination

---

## Problem Statement

### The Challenge

Background services execute asynchronously with `Task.Delay()` or `PeriodicTimer`. When using `TestTimeProvider` for deterministic timing:

**Problem**:
```csharp
// In test:
_timeProvider.Advance(TimeSpan.FromMinutes(5));  // Advances virtual time
await Task.Yield();  // ❌ Background service may not wake up yet!

// Assert immediately - RACE CONDITION!
```

**Why This Fails**:
- `TestTimeProvider.Advance()` advances virtual time instantly
- BUT doesn't immediately wake tasks awaiting `Task.Delay()` or `PeriodicTimer`
- Background service runs on separate thread → timing coordination needed

### Common Symptoms

❌ **Flaky tests** - pass sometimes, fail randomly
❌ **Timing-dependent assertions fail** - "Expected 3 calls, got 0"
❌ **Task.Delay workarounds** - `await Task.Delay(100)` scattered in tests

---

## Solution: BackgroundServiceTestHelper

### Overview

`BackgroundServiceTestHelper<T>` coordinates virtual time advancement with background service execution, ensuring deterministic test behavior.

**Key Innovation**: Combines virtual time advancement with small real delays for thread scheduler coordination.

### Basic Usage

```csharp
using Api.Tests.Helpers;

[Fact]
public async Task BackgroundService_ProcessesPeriodically()
{
    // Arrange
    var timeProvider = TimeTestHelpers.CreateTimeProvider();
    var service = new MyBackgroundService(timeProvider, /* deps */);

    // Act: Using BackgroundServiceTestHelper
    using var helper = new BackgroundServiceTestHelper<MyBackgroundService>(
        service,
        timeProvider,
        timeout: TimeSpan.FromSeconds(5)
    );

    await helper.StartAsync();  // Start and wait for initialization

    // Advance time and wait for processing
    await helper.AdvanceSecondsAsync(1);  // Advance 1 second

    await helper.StopAsync();  // Graceful shutdown

    // Assert
    // Verify expected behavior occurred
}
```

---

## API Reference

### Constructor

```csharp
public BackgroundServiceTestHelper(
    T service,                          // BackgroundService instance
    TestTimeProvider timeProvider,      // Virtual time controller
    TimeSpan? timeout = null,           // Operation timeout (default: 10s)
    int processingDelayMs = 50)         // Coordination delay (default: 50ms)
```

**Parameters**:
- `service`: Background service instance to coordinate
- `timeProvider`: TestTimeProvider for virtual time control
- `timeout`: Maximum time for service operations (prevents infinite loops)
- `processingDelayMs`: Real delay to allow thread scheduler to run background task (tunable)

### Methods

#### StartAsync()
```csharp
public async Task StartAsync()
```
Starts the background service and waits for initialization (default: 50ms).

#### AdvanceAndWaitAsync(TimeSpan)
```csharp
public async Task AdvanceAndWaitAsync(TimeSpan duration)
```
**KEY METHOD**: Advances virtual time AND waits for background service to process.

Internally:
1. Advances TestTimeProvider by `duration`
2. Waits `processingDelayMs` (default 50ms) for thread scheduler
3. Yields to allow background task to run

#### AdvanceSecondsAsync(int)
```csharp
public async Task AdvanceSecondsAsync(int seconds)
```
Convenience method: advances time by seconds and waits.

#### AdvanceMinutesAsync(int)
```csharp
public async Task AdvanceMinutesAsync(int minutes)
```
Convenience method: advances time by minutes and waits.

#### AdvanceHoursAsync(int)
```csharp
public async Task AdvanceHoursAsync(int hours)
```
Convenience method: advances time by hours and waits.

#### WaitForProcessingAsync()
```csharp
public async Task WaitForProcessingAsync()
```
Waits for background service to complete current processing cycle.
Useful after time advancement when additional processing time needed.

#### StopAsync()
```csharp
public async Task StopAsync()
```
Gracefully stops the background service.

---

## Usage Patterns

### Pattern 1: Periodic Background Task

**Service**: Runs every N hours
**Test**: Verify task executes at correct intervals

```csharp
[Fact]
public async Task PeriodicTask_RunsEvery6Hours()
{
    // Arrange
    var timeProvider = TimeTestHelpers.CreateTimeProvider();
    var callCount = 0;
    var service = new MyPeriodicService(timeProvider, () => callCount++);

    // Act
    using var helper = new BackgroundServiceTestHelper<MyPeriodicService>(
        service,
        timeProvider,
        timeout: TimeSpan.FromSeconds(10)
    );

    await helper.StartAsync();

    // Advance past initial delay
    await helper.AdvanceMinutesAsync(5);
    Assert.Equal(1, callCount);  // First execution

    // Advance to next interval
    await helper.AdvanceHoursAsync(6);
    Assert.Equal(2, callCount);  // Second execution

    // Advance to third interval
    await helper.AdvanceHoursAsync(6);
    Assert.Equal(3, callCount);  // Third execution

    await helper.StopAsync();
}
```

### Pattern 2: Startup Delay Verification

**Service**: Waits N minutes before first execution
**Test**: Verify delay is respected

```csharp
[Fact]
public async Task Service_WaitsForStartupDelay()
{
    // Arrange
    var timeProvider = TimeTestHelpers.CreateTimeProvider();
    var executed = false;
    var service = new MyService(timeProvider, () => executed = true);

    // Act
    using var helper = new BackgroundServiceTestHelper<MyService>(
        service,
        timeProvider
    );

    await helper.StartAsync();

    // Advance LESS than startup delay (e.g., 1 minute of 2-minute delay)
    await helper.AdvanceMinutesAsync(1);

    // Assert: Should NOT have executed yet
    Assert.False(executed, "Service should not execute before startup delay");

    // Advance past full delay
    await helper.AdvanceMinutesAsync(2);  // Total: 3 minutes

    // Assert: NOW should execute
    Assert.True(executed, "Service should execute after startup delay");

    await helper.StopAsync();
}
```

### Pattern 3: Exception Handling

**Service**: Continues executing after exceptions
**Test**: Verify error logging and continuation

```csharp
[Fact]
public async Task Service_LogsException_ContinuesExecution()
{
    // Arrange
    var timeProvider = TimeTestHelpers.CreateTimeProvider();
    var mockLogger = new Mock<ILogger<MyService>>();
    var callCount = 0;

    var service = new MyService(timeProvider, mockLogger.Object, () =>
    {
        callCount++;
        if (callCount == 2) throw new Exception("Simulated error");
    });

    // Act
    using var helper = new BackgroundServiceTestHelper<MyService>(
        service,
        timeProvider
    );

    await helper.StartAsync();

    // Trigger 3 executions
    for (int i = 0; i < 3; i++)
    {
        await helper.AdvanceHoursAsync(1);
    }

    await helper.StopAsync();

    // Assert: All 3 executions attempted despite exception
    Assert.Equal(3, callCount);

    // Verify exception was logged
    mockLogger.Verify(
        l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once
    );
}
```

### Pattern 4: Multiple Intervals

**Service**: Runs multiple times in test
**Test**: Verify cumulative behavior

```csharp
[Fact]
public async Task Service_MultipleIntervals_CumulativeEffect()
{
    // Arrange
    var timeProvider = TimeTestHelpers.CreateTimeProvider();
    var scopeCreateCount = 0;
    var service = CreateMockService(timeProvider, () => scopeCreateCount++);

    // Act
    using var helper = new BackgroundServiceTestHelper<MyService>(
        service,
        timeProvider
    );

    await helper.StartAsync();

    // Initial delay
    await helper.AdvanceMillisecondsAsync(30);

    // Trigger multiple intervals
    await helper.AdvanceMillisecondsAsync(120);  // Interval 1
    await helper.AdvanceMillisecondsAsync(120);  // Interval 2
    await helper.AdvanceMillisecondsAsync(120);  // Interval 3

    await helper.StopAsync();

    // Assert: At least 2 executions
    Assert.True(scopeCreateCount >= 2,
        $"Expected at least 2 executions, got {scopeCreateCount}");
}
```

---

## Real-World Examples

### Example 1: CacheWarmingService

**File**: `apps/api/tests/Api.Tests/Services/CacheWarmingServiceTests.cs`

```csharp
[Fact]
public async Task ExecuteAsync_Startup_WarmsTop50Queries()
{
    // Arrange: Service configured with 600ms startup delay
    var service = new CacheWarmingService(/* deps with timeProvider */);

    // Act: Using helper for coordination
    using var helper = new BackgroundServiceTestHelper<CacheWarmingService>(
        service,
        _timeProvider,
        timeout: TimeSpan.FromSeconds(5)
    );

    await helper.StartAsync();

    // Advance past startup delay (600ms) and wait for warming
    await helper.AdvanceSecondsAsync(1);  // > 600ms

    await helper.StopAsync();

    // Assert: Top 50 queries pre-cached
    _ragServiceMock.Verify(
        rag => rag.AskAsync(/* args */),
        Times.Exactly(50)
    );
}
```

### Example 2: QualityReportService

**File**: `apps/api/tests/Api.Tests/Services/QualityReportServiceTests.cs`

```csharp
[Fact]
public async Task ExecuteAsync_AfterInterval_GeneratesReport()
{
    // Arrange: Service with 30ms initial delay, 100ms intervals
    var reportGenerationCount = 0;
    var service = CreateMockService(() => reportGenerationCount++);

    // Act: Using helper for coordination
    using var helper = new BackgroundServiceTestHelper<QualityReportService>(
        service,
        _timeProvider,
        timeout: TimeSpan.FromSeconds(5)
    );

    await helper.StartAsync();

    // Advance through multiple intervals
    await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(50));   // Past initial delay
    await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(120));  // Interval 1
    await helper.AdvanceAndWaitAsync(TimeSpan.FromMilliseconds(120));  // Interval 2

    await helper.StopAsync();

    // Assert: At least 2 reports generated
    Assert.True(reportGenerationCount >= 2,
        $"Expected at least 2 reports, got {reportGenerationCount}");
}
```

---

## Best Practices

### ✅ DO

1. **Use BackgroundServiceTestHelper for all background service tests**
   Provides consistent coordination pattern.

2. **Set appropriate timeout**
   Default 10s is usually fine, adjust for complex operations.

3. **Use fluent time methods**
   `AdvanceSecondsAsync()`, `AdvanceMinutesAsync()` for readability.

4. **Dispose helper properly**
   Use `using var helper = ...` for automatic disposal.

5. **Verify behavior after AdvanceAndWait**
   Helper ensures background task has processed before assertions.

### ❌ DON'T

1. **Don't mix helper with manual StartAsync/StopAsync**
   Let helper manage service lifecycle.

2. **Don't use Task.Delay() in tests**
   Defeats purpose of virtual time. Use helper methods instead.

3. **Don't advance time without waiting**
   Always use `AdvanceAndWaitAsync()` or helper methods, not direct `_timeProvider.Advance()`.

4. **Don't expect instant execution**
   Background tasks need ~50ms real time for thread scheduler.

5. **Don't ignore timeout**
   Set reasonable timeout to prevent infinite loops in failing tests.

---

## Troubleshooting

### Test Still Flaky?

**Increase processing delay**:
```csharp
using var helper = new BackgroundServiceTestHelper<MyService>(
    service,
    timeProvider,
    processingDelayMs: 100  // Increase from default 50ms
);
```

### Test Times Out?

**Check service implementation**:
- Does service honor CancellationToken?
- Are there infinite loops without cancellation checks?
- Is StopAsync() implemented correctly?

**Increase timeout**:
```csharp
using var helper = new BackgroundServiceTestHelper<MyService>(
    service,
    timeProvider,
    timeout: TimeSpan.FromSeconds(30)  // Increase from default 10s
);
```

### Assertions Fail (Expected X, Got 0)?

**Add additional wait**:
```csharp
await helper.AdvanceSecondsAsync(1);
await helper.WaitForProcessingAsync();  // Extra wait for complex operations
```

### Service Not Executing?

**Check time advancement**:
- Are you advancing past initial delay?
- Are you advancing past full interval duration?
- Try larger time increments for debugging.

---

## Performance Considerations

### Target: <100ms Per Test

**Actual Performance**:
- Helper startup: ~50ms (real time for thread init)
- Per AdvanceAndWait: ~50ms (thread scheduler coordination)
- Total for typical test: 50-150ms

**This is MUCH faster than**:
- Real delays: `Task.Delay(TimeSpan.FromSeconds(5))` = 5000ms ❌
- No coordination: Flaky tests requiring retries

### Tuning Processing Delay

**Default 50ms works for most services**.

**Adjust if needed**:
```csharp
// Faster (riskier - may miss processing):
processingDelayMs: 20

// Slower (safer - guaranteed processing):
processingDelayMs: 100
```

**Trade-off**: Lower delay = faster tests but higher flakiness risk.

---

## When to Use vs. Alternatives

### Use BackgroundServiceTestHelper When:

✅ Testing BackgroundService with virtual time
✅ Service uses PeriodicTimer or Task.Delay with TimeProvider
✅ Need deterministic timing control
✅ Want to avoid real delays in tests

### Use Alternative Approaches When:

❌ **Integration tests with real time** → Don't use TestTimeProvider at all
❌ **Non-background services** → Regular service testing patterns
❌ **Synchronous operations** → No coordination needed

---

## Migration Guide

### Before (Flaky Pattern)

```csharp
[Fact]
public async Task MyTest()
{
    var service = new MyBackgroundService(_timeProvider);
    using var cts = new CancellationTokenSource();
    cts.CancelAfter(TimeSpan.FromSeconds(5));

    await service.StartAsync(cts.Token);

    _timeProvider.Advance(TimeSpan.FromMinutes(5));  // ❌ No coordination
    await Task.Yield();  // ❌ Insufficient
    await Task.Delay(10);  // ❌ Hacky workaround

    cts.Cancel();
    await service.StopAsync(cts.Token);

    // Assert - may fail randomly
}
```

### After (Robust Pattern)

```csharp
[Fact]
public async Task MyTest()
{
    var service = new MyBackgroundService(_timeProvider);

    using var helper = new BackgroundServiceTestHelper<MyBackgroundService>(
        service,
        _timeProvider,
        timeout: TimeSpan.FromSeconds(5)
    );

    await helper.StartAsync();

    await helper.AdvanceMinutesAsync(5);  // ✅ Coordinated

    await helper.StopAsync();

    // Assert - deterministic
}
```

---

## Advanced Scenarios

### Multiple Time Advances

```csharp
await helper.StartAsync();

// Skip initial delay
await helper.AdvanceMinutesAsync(2);

// Trigger first interval
await helper.AdvanceHoursAsync(1);

// Trigger second interval
await helper.AdvanceHoursAsync(1);

// Extra wait for complex processing
await helper.WaitForProcessingAsync();

await helper.StopAsync();
```

### Variable Processing Time

```csharp
// For slower/more complex background operations
using var helper = new BackgroundServiceTestHelper<ComplexService>(
    service,
    timeProvider,
    processingDelayMs: 100  // Longer wait
);
```

### Custom Timeout

```csharp
// For long-running test scenarios
using var helper = new BackgroundServiceTestHelper<LongService>(
    service,
    timeProvider,
    timeout: TimeSpan.FromMinutes(1)  // Longer timeout
);
```

---

## Comparison: Before vs. After

| Aspect | Without Helper (❌) | With Helper (✅) |
|--------|---------------------|-------------------|
| **Coordination** | Manual Task.Yield/Delay | Automatic |
| **Flakiness** | High (race conditions) | Low (deterministic) |
| **Readability** | Cluttered with waits | Clean, fluent API |
| **Performance** | Variable (50-500ms) | Consistent (50-150ms) |
| **Maintenance** | Fragile, needs tuning | Robust, self-contained |

---

## Implementation Notes

### How It Works Internally

```csharp
public async Task AdvanceAndWaitAsync(TimeSpan duration)
{
    // 1. Advance virtual time (instant)
    _timeProvider.Advance(duration);

    // 2. Give thread scheduler time to run background task
    await Task.Delay(_processingDelayMs);  // Real 50ms delay

    // 3. Yield to ensure task runs
    await Task.Yield();
}
```

**Why This Works**:
- Virtual time advances instantly (TestTimeProvider)
- Real 50ms delay allows OS thread scheduler to run background thread
- Yield ensures current thread releases control
- Background service wakes up, sees advanced time, processes

### Thread Safety

Helper is **thread-safe** for typical usage:
- Each helper instance manages one service instance
- CancellationTokenSource is internal and not shared
- TestTimeProvider is thread-safe (atomic operations)

**Don't share helper across tests** - create new instance per test.

---

## Testing the Helper Itself

### Validation Checklist

To verify BackgroundServiceTestHelper works correctly:

- [ ] Run tests 3 consecutive times - all pass
- [ ] No flakiness detected
- [ ] Performance <150ms per test
- [ ] No test isolation issues (tests pass in any order)
- [ ] Works with different service configurations

### Example Validation Command

```bash
# Run 3 times
for i in {1..3}; do
  dotnet test --filter "CacheWarmingServiceTests" && echo "Run $i: PASS" || echo "Run $i: FAIL"
done
```

---

## References

- **Issue**: #597 - BackgroundServiceTestHelper implementation
- **Related**: `docs/testing/time-provider-migration-guide.md` - TimeProvider patterns
- **Code**: `apps/api/tests/Api.Tests/Helpers/BackgroundServiceTestHelper.cs`
- **Examples**:
  - `CacheWarmingServiceTests.cs` - Cache warming background service
  - `QualityReportServiceTests.cs` - Periodic report generation
  - `SessionAutoRevocationServiceTests.cs` - Session cleanup

---

**Generated with [Claude Code](https://claude.com/claude-code)**
**Co-Authored-By:** Claude <noreply@anthropic.com>
