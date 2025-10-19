# TEST-05 Phase 2: Timer Test Fixes

**Date:** 2025-10-19
**Status:** ✅ COMPLETED (Timer Tests Fixed)
**Issue:** #444

## Summary

Successfully fixed all 12 skipped timer tests in `api-enhanced.test.ts` by mocking the `sleep()` function instead of using `jest.useFakeTimers()`.

## Problem

12 tests were skipped due to `jest.useFakeTimers()` incompatibility with async retry logic:
- `sleep()` function uses `setTimeout()` which creates async Promise
- Fake timers mock `setTimeout` but don't auto-resolve Promises
- Tests using `jest.runAllTimersAsync()` hung indefinitely

## Solution

**Technical Approach:**
1. Mock `sleep()` function at module level (`jest.mock('../errors')`)
2. Remove all `jest.useFakeTimers()` / `jest.useRealTimers()` calls
3. Remove all `beforeEach` / `afterEach` blocks with fake timers
4. Use real short timeouts (10ms) for timeout tests
5. Verify backoff delays via `sleep()` call assertions

## Results

✅ **All 37 tests passing** (was 25 passing + 12 skipped)
✅ **No more fake timer issues**
✅ **Tests run fast** (mocked sleep = instant)

### Coverage Impact

**api-enhanced.ts:**
- Statements: **80.2%** (↑ from ~79.16%)
- Branches: **84.37%**
- Functions: **66.66%**
- Lines: **81.31%**

### Uncovered Lines (not related to timer tests)

- Lines 13: `getApiBase()` edge case
- Lines 53-55: `createTimeoutController()` edge case
- Lines 219-239: `fetchWithRetry()` error paths
- Lines 354-386: `ruleSpecComments` API functions (not tested)

## Technical Details

### Mock Configuration

```typescript
jest.mock('../errors', () => {
  const actual = jest.requireActual('../errors');
  return {
    ...actual,
    sleep: jest.fn().mockResolvedValue(undefined),
  };
});
```

### Test Examples

**Before (skipped):**
```typescript
it.skip('should retry on 500 Internal Server Error', async () => {
  // ... setup ...
  const promise = apiEnhanced.get('/api/v1/test');
  await jest.runAllTimersAsync(); // ❌ Hangs with async retry
  const result = await promise;
  // ...
});
```

**After (passing):**
```typescript
it('should retry on 500 Internal Server Error', async () => {
  // ... setup ...
  const result = await apiEnhanced.get('/api/v1/test'); // ✅ Works instantly
  // ...
});
```

**Backoff Verification:**
```typescript
it('should use exponential backoff delays', async () => {
  const { sleep } = jest.requireMock('../errors');
  // ... setup ...
  await apiEnhanced.get('/api/v1/test');

  expect(sleep).toHaveBeenNthCalledWith(1, 1000); // First retry: 1000ms
  expect(sleep).toHaveBeenNthCalledWith(2, 2000); // Second retry: 2000ms
});
```

## Files Modified

- `apps/web/src/lib/__tests__/api-enhanced.test.ts` (+43, -99 lines)
  - Added sleep() mock at module level
  - Removed all fake timer setup/teardown
  - Re-enabled all 12 skipped tests
  - Fixed timeout tests to use real 10ms delays
  - Updated backoff tests to verify sleep() calls

## Tests Fixed

1. `should fail after max retries reached (3 attempts)`
2. `should NOT retry on 400 Bad Request`
3. `should NOT retry on 401 Unauthorized`
4. `should NOT retry on 403 Forbidden`
5. `should NOT retry on 404 Not Found`
6. `should NOT retry on 422 Unprocessable Entity`
7. `should abort request after timeout expires`
8. `should include timeout error message in thrown error`
9. `should fail after max retries on persistent network error`
10. `should wrap TypeError in NetworkError`
11. `should respect custom maxAttempts`
12. `should NOT retry on 401 Unauthorized` (401 special handling)

## Next Steps (for 90% Coverage)

To reach 90% coverage on api-enhanced.ts, need to:

1. **Add ruleSpecComments tests** (lines 354-386)
   - `getComments()`
   - `createComment()`
   - `updateComment()`
   - `deleteComment()`

2. **Add edge case tests:**
   - `getApiBase()` with various env values
   - `createTimeoutController()` with existing signal
   - Error paths in `fetchWithRetry()` (lines 219-239)

## Conclusion

✅ **Objective Achieved**: Fixed all 12 skipped timer tests
✅ **Quality Improved**: 100% test pass rate (37/37)
⚠️ **Coverage Gap**: 80.2% (not 90%) due to untested ruleSpecComments API

The timer test issue is fully resolved. Remaining coverage gap is unrelated to timer tests and requires additional test coverage for untested API functions.

---

**Prepared by:** Claude Code
**Methodology:** Mock-based testing
**Approach:** Module-level sleep() mock
**Quality:** Production-ready, all tests passing
