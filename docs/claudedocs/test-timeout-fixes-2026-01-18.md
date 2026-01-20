# Test Timeout Fixes - Issue #2602

## Problem Summary
~15 tests fail due to timing-sensitive operations with hard-coded delays and timeouts.

## Common Anti-Patterns Found

### 1. Thread.Sleep() Usage (BLOCKING)
```csharp
// ❌ BAD - Blocks thread
Thread.Sleep(10);

// ✅ GOOD - Use TestTimeProvider or TestConstants
TimeProvider.Advance(TimeSpan.FromMilliseconds(10));
// OR
await Task.Delay(TestConstants.Timing.TinyDelay);
```

**Files with Thread.Sleep()**:
- `ApiKeyEntityTests.cs` (line ~10)
- `OAuthAccountEntityTests.cs` (line ~10)
- `SessionEntityTests.cs` (line ~10)
- `DocumentCollectionTests.cs` (line ~10)
- `GameSessionStateDomainTests.cs` (line ~10)

### 2. Hard-coded Task.Delay() (FLAKY)
```csharp
// ❌ BAD - Magic numbers, flaky
await Task.Delay(200);
await Task.Delay(1500);

// ✅ GOOD - Use TestConstants.Timing
await Task.Delay(TestConstants.Timing.SmallDelay);
await Task.Delay(TestConstants.Timing.LongTimeout);
```

**Files with hard-coded delays**:
- `ShareLinkTokenTests.cs` - `await Task.Delay(200)`
- `ValidateShareLinkQueryHandlerTests.cs` - `await Task.Delay(1500)`
- `RevokeShareLinkCommandHandlerTests.cs` - `await Task.Delay(10)`

### 3. Hard-coded Timeout Values
```csharp
// ❌ BAD - Magic timeout
using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

// ✅ GOOD - Use TestConstants.Timing
using var cts = new CancellationTokenSource(TestConstants.Timing.ShortTimeout);
```

**Files with hard-coded timeouts**:
- `SimulateErrorCommandHandlerTests.cs` - `TimeSpan.FromMilliseconds(100)`

## TestConstants.Timing Reference

Available timing constants (from `Api.Tests/TestConstants.cs`):

```csharp
// Timeouts
DefaultTimeout = 30s
ShortTimeout = 5s
LongTimeout = 5min

// Delays
MinimalDelay = 1ms
TinyDelay = 10ms
SmallDelay = 100ms
MediumDelay = 150ms
LargeDelay = 300ms
RetryDelay = 500ms

// Tolerances
AssertionTolerance = 10s
StrictAssertionTolerance = 2s
```

## TestTimeProvider Pattern (Deterministic)

For tests requiring precise time control without actual delays:

```csharp
// Setup
private readonly TestTimeProvider _timeProvider = new();

// In test
var session = new Session(_timeProvider);
_timeProvider.Advance(TimeSpan.FromMinutes(31));  // Instant!
Assert.True(session.IsExpired);
```

**Benefits**:
- ✅ Deterministic (no flaky tests)
- ✅ Fast (no actual waiting)
- ✅ Thread-safe
- ✅ Reproducible

## Quick Fix Strategy

### Priority 1: Thread.Sleep() → TestTimeProvider (5 files)
Replace all `Thread.Sleep(10)` with TestTimeProvider advancement.

### Priority 2: Hard-coded delays → TestConstants (3 files)
Replace `Task.Delay(200)`, `Task.Delay(1500)` with TestConstants.

### Priority 3: Hard-coded timeouts → TestConstants (1 file)
Replace `TimeSpan.FromMilliseconds(100)` with TestConstants.Timing.

## Implementation Status

- [x] Created MockReportFormatter for #2601
- [x] Documented timeout anti-patterns
- [ ] Fix Thread.Sleep() in 5 domain entity tests
- [ ] Fix hard-coded Task.Delay() in 3 tests
- [ ] Fix hard-coded timeout in SimulateErrorCommandHandlerTests
- [ ] Verify with `dotnet test --filter "Timeout|Timed|Workflow"`

## Success Metric
Target: 15 tests fixed → Pass rate improvement from 99.5% to 99.7% (+0.2%)

---
**Issue**: #2602
**Date**: 2026-01-18
**Status**: In Progress
