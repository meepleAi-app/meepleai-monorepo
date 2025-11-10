# ProcessingProgress Network Error Display Fix

**Issue ID**: test-improvements-p2
**Date**: 2025-10-30
**Status**: ✅ Fixed

## Problem

Two tests were failing because the network error message was not rendering:

1. `ProcessingProgress.test.tsx` line ~323-326: "should display network error when API call fails"
2. `ProcessingProgress-fixed.test.tsx` line ~126-129: "should display network error when API call fails"

**Error Pattern**:
```
Unable to find role="alert"
Unable to find an element with the text: /Network Error:/i
```

## Root Cause Analysis

### Component Logic Issue

The network error message was conditionally rendered **only when `progress` data exists**:

```typescript
// BEFORE (Line 428-477)
{progress && (
  <div>
    ...
    {/* Network Error */}
    {networkError && (  // <-- Only renders if progress exists
      <div style={networkErrorStyle} role="alert">
        <strong>Network Error:</strong> {networkError}
      </div>
    )}
  </div>
)}
```

### Why This Failed

When the API call fails on the **initial fetch**:
1. `fetchProgress()` throws an error (line 111-119)
2. `networkError` state is set to the error message (line 117)
3. `progress` remains `null` because no data was fetched (line 51)
4. Component renders but **skips the error div** because it's inside `{progress && ...}` block (line 428)

### Key Insight

This is **not a timing issue** - it's a **conditional rendering logic bug**:
- The error state is correctly set in state
- The component is rendering correctly
- But the error message div is hidden behind a conditional that prevents it from displaying

## The Fix

**File**: `apps/web/src/components/ProcessingProgress.tsx`

**Change**: Move network error display **outside** the `progress` conditional block:

```typescript
// AFTER (Line 427-433)
{/* Network Error - Display even when progress is null */}
{/* This allows errors to be shown during initial fetch failures */}
{networkError && (
  <div style={networkErrorStyle} role="alert">
    <strong>Network Error:</strong> {networkError}
  </div>
)}

{/* Current Status */}
{progress && (
  <div>
    ...
    {/* Processing failure errors remain inside progress block */}
    {progress.currentStep === ProcessingStep.Failed && progress.errorMessage && (
      <div style={errorMessageStyle} role="alert">
        <strong>Error:</strong> {progress.errorMessage}
      </div>
    )}
  </div>
)}
```

## Error Display Architecture

The component now correctly handles **two types of errors**:

### 1. Network Errors (API failures)
- **Display condition**: `networkError` state is set
- **Location**: Outside `progress` conditional (line 427-433)
- **Examples**:
  - Initial fetch failure
  - Network timeout during polling
  - API endpoint unreachable
  - Cancel operation failure (line 196)
- **Style**: Orange warning box (`networkErrorStyle`)

### 2. Processing Errors (PDF processing failures)
- **Display condition**: `progress.currentStep === Failed` AND `progress.errorMessage` exists
- **Location**: Inside `progress` conditional (line 457-462)
- **Examples**:
  - PDF parsing failure
  - Embedding generation failure
  - Vector indexing failure
- **Style**: Red error box (`errorMessageStyle`)

## Test Results

### Before Fix
```
FAIL src/components/__tests__/ProcessingProgress.test.tsx
  × should display network error when API call fails (114 ms)

FAIL src/components/__tests__/ProcessingProgress-fixed.test.tsx
  × should display network error when API call fails

Tests: 2 failed, 28 passed, 30 total
```

### After Fix
```
PASS src/components/__tests__/ProcessingProgress.test.tsx
  √ should display network error when API call fails (74 ms)

PASS src/components/__tests__/ProcessingProgress-fixed.test.tsx
  √ should display network error when API call fails (65 ms)

Tests: 34 passed, 34 total
```

## Validation Checklist

✅ **Network error displays on initial fetch failure**
- Error message appears even when `progress` is null
- Alert role is correctly applied
- Error text matches expected format

✅ **Processing errors still work correctly**
- Error messages display when `progress.currentStep === Failed`
- Different styling (red) from network errors (orange)
- No regression in existing error handling

✅ **No side effects**
- All 34 tests pass (30 in main file, 4 in -fixed file)
- Loading states work correctly
- Polling behavior unchanged
- Cancel functionality unchanged

✅ **Accessibility maintained**
- `role="alert"` on network error div
- Screen reader friendly error messages
- Proper semantic HTML structure

## Key Learnings

### 1. Conditional Rendering Pitfalls
When displaying error states, be careful about nesting them inside conditional blocks that depend on success state. Errors should often be displayed **independently** of data presence.

### 2. Error State Independence
Network errors and processing errors have different lifecycles:
- **Network errors**: Can occur before any data is fetched
- **Processing errors**: Only occur after data fetching succeeds

### 3. Test-Driven Debugging
The test output clearly showed the component was rendering but the error message was missing. This pointed directly to a conditional rendering issue, not a timing or state management problem.

### 4. Comments for Future Maintainers
Added explanatory comments (line 427-428) to document **why** the network error is outside the progress conditional, preventing future regressions.

## Impact

- **Tests fixed**: 2 (network error display tests)
- **Component reliability**: Improved error visibility for users
- **Code clarity**: Clear separation between network and processing errors
- **Accessibility**: Maintained WCAG compliance with proper alert roles

## Related Issues

- **Phase 1.3**: Fixed act() warnings in ProcessingProgress tests
- **This Fix (Phase 1.4)**: Fixed network error display logic
- **Next**: Continue with Phase 2 (Integration tests) and Phase 3 (E2E tests)
