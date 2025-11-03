# Auto-Save Test Strategy for RuleSpecEditor

## Current Situation
- **Coverage**: 82.08% (target: 90%)
- **Main Uncovered Block**: Lines 166-188 (handleAutoSave function)
- **Tests Passing**: 30/31

## Root Cause Analysis

### Why Lines 166-188 Aren't Covered

The auto-save flow has these dependencies:
```typescript
// Line 60: Debounced content (2-second delay)
const debouncedContent = useDebounce(viewMode === "rich" ? richContent : jsonContent, 2000);

// Lines 158-163: Auto-save effect
useEffect(() => {
  if (hasUnsavedChanges && isValid && debouncedContent) {
    void handleAutoSave();  // This triggers lines 166-188
  }
}, [debouncedContent]);
```

**The Problem**:
1. `useDebounce` hook uses real `setTimeout` with 2-second delay
2. Tests change content but don't wait long enough for debounce
3. Effect depends on `hasUnsavedChanges && isValid && debouncedContent`
4. Using `jest.useFakeTimers()` breaks React's rendering cycle

## Recommended Test Strategy

### Option 1: Real Timers (Simple, Reliable)

**Pros**:
- Works with React's natural rendering cycle
- No complex timer mocking needed
- Tests real user behavior

**Cons**:
- Tests are slower (2+ seconds each)
- Requires careful timeout management

```typescript
it('should trigger auto-save after 2 second debounce', async () => {
  const updatedSpec = { ...sampleRuleSpec, version: '1.0.1' };
  let autoSaveCalled = false;

  router.put('/api/v1/games/game-1/rulespec', async () => {
    autoSaveCalled = true;
    return createJsonResponse(updatedSpec);
  });

  render(<RuleSpecEditor />);
  await waitForEditorReady();

  const textarea = getEditorTextarea();
  fireEvent.change(textarea, {
    target: { value: JSON.stringify(updatedSpec, null, 2) }
  });

  // Wait for debounce (2s) + auto-save completion
  await waitFor(
    () => expect(autoSaveCalled).toBe(true),
    { timeout: 3000 } // 2s debounce + 1s buffer
  );

  // Verify success message
  expect(screen.getByText(/Auto-salvato/i)).toBeInTheDocument();
});
```

### Option 2: Mock useDebounce Hook

**Pros**:
- Tests run instantly
- Precise control over timing

**Cons**:
- Not testing real debounce behavior
- Additional mock setup required

```typescript
// At file level
jest.mock('../../hooks/useDebounce', () => ({
  useDebounce: <T,>(value: T, _delay: number) => value // Return immediately
}));
```

### Option 3: Hybrid Approach (RECOMMENDED)

Use **mock for most tests**, **real timers for critical path**:

```typescript
// Mock debounce for fast tests
jest.mock('../../hooks/useDebounce');

describe('Auto-Save - Fast Tests', () => {
  beforeEach(() => {
    const { useDebounce } = require('../../hooks/useDebounce');
    useDebounce.mockImplementation((value) => value); // Immediate
  });

  it('validates auto-save conditions quickly', async () => {
    // Fast test without waiting
  });
});

describe('Auto-Save - Integration Tests', () => {
  beforeEach(() => {
    jest.unmock('../../hooks/useDebounce'); // Use real debounce
  });

  it('performs full auto-save flow with real timing', async () => {
    // Real 2-second wait
  }, 5000); // Increase Jest timeout for this test
});
```

## Complete Test Coverage Plan

### Lines to Cover

| Lines | Feature | Test Strategy |
|-------|---------|---------------|
| 166-168 | Early return (invalid/missing) | Mock debounce, test conditions |
| 171 | Rich-to-JSON conversion | Mock debounce, test viewMode='rich' |
| 172 | JSON parsing | Mock debounce, valid JSON input |
| 174 | setIsSaving(true) | Mock debounce, check button state |
| 175 | Clear error message | Mock debounce, verify no error |
| 177 | API PUT call | Mock debounce, spy on fetch |
| 178 | Update ruleSpec state | Mock debounce, check preview |
| 179 | Clear unsaved changes | Mock debounce, check indicator gone |
| 180 | Success message | Mock debounce, check text content |
| 183 | Clear message after 3s | Real timers or `jest.advanceTimersByTime(3000)` |
| 185 | Error logging | Mock debounce, trigger API error |
| 186 | Silent error handling | Mock debounce, verify no UI error |
| 188 | setIsSaving(false) finally | Mock debounce, trigger error, check state |

### Recommended Test Suite

```typescript
describe('RuleSpecEditor - Auto-Save', () => {
  // Mock debounce for fast tests
  jest.mock('../../hooks/useDebounce', () => ({
    useDebounce: (value: any) => value
  }));

  describe('Auto-Save Triggering', () => {
    it('triggers when hasUnsavedChanges && isValid && debouncedContent', () => {
      // Lines 158-163
    });

    it('does NOT trigger when content invalid', () => {
      // Lines 158-163, 166-168
    });

    it('does NOT trigger when no unsaved changes', () => {
      // Lines 158-163
    });
  });

  describe('handleAutoSave Execution', () => {
    it('returns early when isValid=false', () => {
      // Lines 166-168
    });

    it('returns early when gameId missing', () => {
      // Lines 166-168
    });

    it('converts rich content to JSON', () => {
      // Line 171
    });

    it('sets isSaving during save', () => {
      // Line 174, 188
    });

    it('clears error message', () => {
      // Line 175
    });

    it('calls API with parsed JSON', () => {
      // Lines 172, 177
    });

    it('updates ruleSpec state', () => {
      // Line 178
    });

    it('clears hasUnsavedChanges', () => {
      // Line 179
    });

    it('shows success message with version', () => {
      // Line 180
    });

    it('handles API errors silently', () => {
      // Lines 184-186
    });

    it('clears isSaving even on error', () => {
      // Line 188 (finally)
    });
  });

  describe('Success Message Timeout', () => {
    // Use real timers or jest.useFakeTimers() for this one test
    jest.useFakeTimers();

    it('clears success message after 3 seconds', () => {
      // Line 183
      jest.advanceTimersByTime(3000);
    });

    jest.useRealTimers();
  });
});
```

## Implementation Steps

1. **Fix broken test** in `editor.test.tsx` (line 452 - remove `mockedApi`)
2. **Choose debounce strategy** (recommend hybrid approach)
3. **Add auto-save tests** covering lines 166-188
4. **Add undo/redo tests** for lines 246-261
5. **Add view switching tests** for lines 210-214
6. **Run coverage**: `pnpm test:coverage -- editor`
7. **Verify 90%+ coverage** achieved

## Quick Wins for 90% Coverage

Focus on these high-impact tests:

1. ✅ **Fix broken test** (line 452) - DONE
2. **Auto-save with mocked debounce** (5 tests = ~5% coverage)
3. **Undo/Redo** (2 tests = ~2% coverage)
4. **View mode switching** (2 tests = ~1% coverage)

**Estimated New Coverage**: 82% + 8% = **90%** ✅

## Files Created

1. `editor-autosave.test.tsx` - Auto-save tests (13 tests, needs timer fix)
2. `editor-additional.test.tsx` - Undo/redo/view switching (20 tests)
3. `AUTO_SAVE_TEST_STRATEGY.md` - This document

## Next Actions

1. **Update `editor-autosave.test.tsx`**:
   - Remove all `jest.useFakeTimers()` and `jest.advanceTimersByTime()`
   - Either use real `waitFor` with 3s timeout OR mock useDebounce

2. **Example fix**:
```typescript
// Option A: Mock useDebounce (at file top)
jest.mock('../../hooks/useDebounce', () => ({
  useDebounce: (value: any) => value
}));

// Then tests work instantly without waiting

// Option B: Real timers (in each test)
await waitFor(
  () => expect(autoSaveCalled).toBe(true),
  { timeout: 3000 }
);
```

3. **Run tests**:
```bash
pnpm test:coverage -- "editor*.test.tsx"
```

4. **Verify coverage** >= 90%

## Coverage Report Format

Look for this in output:
```
File          | % Stmts | % Branch | % Funcs | % Lines | Uncovered Line #s
editor.tsx    |   90.xx |    88.xx |   91.xx |   90.xx | 115,280
```

## Success Criteria

- [ ] All tests passing
- [ ] Statement coverage ≥ 90%
- [ ] Branch coverage ≥ 85%
- [ ] Lines 166-188 covered
- [ ] No flaky tests (consistent results)
