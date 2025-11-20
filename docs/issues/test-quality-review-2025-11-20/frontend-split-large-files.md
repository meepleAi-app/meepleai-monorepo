# Issue: Split Large Frontend Test Files

**ID**: TEST-006
**Category**: Frontend Testing - Maintainability
**Priority**: 🔴 **CRITICAL**
**Status**: 🔴 Open
**Created**: 2025-11-20

---

## 📋 Summary

Split oversized test files (>500 lines) into smaller, focused modules. Currently, `useChatStreaming.test.ts` is **1,234 lines** - too large for effective maintenance, review, and navigation.

---

## 🎯 Problem Statement

### Current Issues

**File**: `apps/web/src/lib/hooks/__tests__/useChatStreaming.test.ts`
- **1,234 lines** (2.5x recommended maximum)
- **11 describe blocks** covering different concerns
- **80+ test cases** in single file
- **Difficult navigation**: Scrolling to find specific tests
- **Long CI time**: Single file runs sequentially
- **Merge conflicts**: High traffic file
- **Code review**: Overwhelming diffs

### Test Distribution
```typescript
describe('useChatStreaming', () => {
  describe('Initial State', () => {})            // 30 lines
  describe('Starting Stream', () => {})          // 80 lines
  describe('Receiving Events', () => {})         // 200 lines
  describe('Callbacks', () => {})                // 120 lines
  describe('Stop Streaming', () => {})           // 100 lines
  describe('Reset', () => {})                    // 80 lines
  describe('HTTP Error Handling', () => {})      // 100 lines
  describe('Event Format Edge Cases', () => {})  // 180 lines
  describe('Reset Functionality', () => {})      // 60 lines
  describe('AbortController Handling', () => {}) // 120 lines
  describe('Stream Completion', () => {})        // 100 lines
  describe('Multiple Concurrent Streams', () => {}) // 120 lines
  describe('Streaming State Transitions', () => {}) // 144 lines
}
```

### Impact
- ⚠️ **Maintenance burden**: Hard to find and update tests
- ⚠️ **Review fatigue**: Reviewers skip thorough checks
- ⚠️ **Merge conflicts**: Multiple devs touching same file
- ⚠️ **CI bottleneck**: Sequential execution
- ⚠️ **Onboarding difficulty**: New devs overwhelmed

---

## 🔧 Solution: Split into Focused Modules

### Recommended Structure

```
apps/web/src/lib/hooks/__tests__/useChatStreaming/
├── setup.ts                          # Shared test utilities
├── initial-state.test.ts             # 50 lines
├── streaming-control.test.ts         # 150 lines (start/stop/reset)
├── event-handling.test.ts            # 250 lines (tokens, citations, complete)
├── callbacks.test.ts                 # 150 lines (onComplete, onError)
├── error-handling.test.ts            # 200 lines (HTTP errors, SSE errors)
├── edge-cases.test.ts                # 200 lines (malformed, partial chunks)
├── abort-controller.test.ts          # 150 lines (cancellation)
└── concurrent-streams.test.ts        # 120 lines (multiple streams)
```

### Shared Setup File

**File**: `useChatStreaming/setup.ts`
```typescript
import { renderHook, act, waitFor } from '@testing-library/react';
import { useChatStreaming } from '../../useChatStreaming';
import { SSEMockBuilder } from '@/__tests__/helpers/sse-mock-builder';
import { TextEncoder, TextDecoder } from 'util';

// Polyfill for jsdom
global.TextEncoder = TextEncoder as any;
global.TextDecoder = TextDecoder as any;

// Mock fetch globally
export const mockFetch = jest.fn();
global.fetch = mockFetch;

/**
 * Render useChatStreaming hook with optional callbacks
 */
export function renderChatStreaming(options?: {
  onComplete?: jest.Mock;
  onError?: jest.Mock;
}) {
  return renderHook(() => useChatStreaming(options));
}

/**
 * Wait for streaming to complete
 */
export async function waitForStreamComplete(result: any) {
  await waitFor(() => {
    const [state] = result.current;
    expect(state.isStreaming).toBe(false);
  });
}

/**
 * Create simple token stream for basic tests
 */
export function createSimpleTokenStream(...tokens: string[]) {
  const builder = new SSEMockBuilder();
  tokens.forEach(token => builder.addToken(token));
  builder.addComplete({ totalTokens: tokens.length, confidence: 0.9, snippets: [] });
  return builder.build();
}

beforeEach(() => {
  jest.clearAllMocks();
  mockFetch.mockReset();
});
```

### Example Split: initial-state.test.ts

**File**: `useChatStreaming/initial-state.test.ts`
```typescript
import { renderChatStreaming } from './setup';

describe('useChatStreaming - Initial State', () => {
  it('should have correct initial state', () => {
    const { result } = renderChatStreaming();
    const [state] = result.current;

    expect(state.state).toBeNull();
    expect(state.currentAnswer).toBe('');
    expect(state.snippets).toEqual([]);
    expect(state.totalTokens).toBe(0);
    expect(state.confidence).toBeNull();
    expect(state.isStreaming).toBe(false);
    expect(state.error).toBeNull();
  });

  it('should provide control functions', () => {
    const { result } = renderChatStreaming();
    const [, controls] = result.current;

    expect(controls.startStreaming).toBeDefined();
    expect(controls.stopStreaming).toBeDefined();
    expect(controls.reset).toBeDefined();
    expect(typeof controls.startStreaming).toBe('function');
    expect(typeof controls.stopStreaming).toBe('function');
    expect(typeof controls.reset).toBe('function');
  });
});
```

### Example Split: event-handling.test.ts

**File**: `useChatStreaming/event-handling.test.ts`
```typescript
import { act, waitFor } from '@testing-library/react';
import { renderChatStreaming, mockFetch, createSimpleTokenStream } from './setup';
import { SSEMockBuilder } from '@/__tests__/helpers/sse-mock-builder';

describe('useChatStreaming - Event Handling', () => {
  describe('State Updates', () => {
    it('should handle stateUpdate events', async () => {
      const mock = new SSEMockBuilder()
        .addStateUpdate('Generating embeddings...')
        .build();

      mockFetch.mockResolvedValue(mock);

      const { result } = renderChatStreaming();
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.state).toBe('Generating embeddings...');
      });
    });
  });

  describe('Token Accumulation', () => {
    it('should accumulate tokens', async () => {
      mockFetch.mockResolvedValue(createSimpleTokenStream('Hello', ' ', 'world'));

      const { result } = renderChatStreaming();
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.currentAnswer).toBe('Hello world');
      });
    });
  });

  describe('Citations', () => {
    it('should handle citations events', async () => {
      const testSnippets = [
        { text: 'Rule 1', source: 'rules.pdf', page: 1, line: null },
        { text: 'Rule 2', source: 'rules.pdf', page: 2, line: null },
      ];

      const mock = new SSEMockBuilder()
        .addCitations(testSnippets)
        .build();

      mockFetch.mockResolvedValue(mock);

      const { result } = renderChatStreaming();
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.snippets).toEqual(testSnippets);
      });
    });
  });

  describe('Completion', () => {
    it('should handle complete event', async () => {
      const mock = new SSEMockBuilder()
        .addToken('Answer')
        .addComplete({ totalTokens: 50, confidence: 0.87, snippets: [] })
        .build();

      mockFetch.mockResolvedValue(mock);

      const { result } = renderChatStreaming();
      const [, controls] = result.current;

      act(() => {
        controls.startStreaming('game-1', 'test query');
      });

      await waitFor(() => {
        const [state] = result.current;
        expect(state.isStreaming).toBe(false);
        expect(state.totalTokens).toBe(50);
        expect(state.confidence).toBe(0.87);
      });
    });
  });
});
```

---

## 📝 Implementation Checklist

### Phase 1: Setup (1 hour)
- [ ] Create directory `apps/web/src/lib/hooks/__tests__/useChatStreaming/`
- [ ] Create `setup.ts` with shared utilities
- [ ] Export common test helpers
- [ ] Update Jest config if needed (should work automatically)

### Phase 2: Split Files (2-3 hours)
- [ ] Create `initial-state.test.ts` (30 lines)
- [ ] Create `streaming-control.test.ts` (150 lines)
- [ ] Create `event-handling.test.ts` (250 lines)
- [ ] Create `callbacks.test.ts` (150 lines)
- [ ] Create `error-handling.test.ts` (200 lines)
- [ ] Create `edge-cases.test.ts` (200 lines)
- [ ] Create `abort-controller.test.ts` (150 lines)
- [ ] Create `concurrent-streams.test.ts` (120 lines)

### Phase 3: Migrate Tests (1-2 hours)
- [ ] Copy-paste relevant describe blocks to new files
- [ ] Update imports to use shared setup
- [ ] Remove duplicated setup code
- [ ] Verify all tests pass individually
- [ ] Verify all tests pass together

### Phase 4: Cleanup (1 hour)
- [ ] Delete original `useChatStreaming.test.ts`
- [ ] Update imports in related files (if any)
- [ ] Update test documentation
- [ ] Commit with clear message explaining split

---

## ✅ Acceptance Criteria

- [ ] No test file >500 lines
- [ ] All tests from original file preserved
- [ ] All tests pass with same coverage
- [ ] Shared setup reduces duplication
- [ ] Each file has clear, focused responsibility
- [ ] Navigation improved (easier to find tests)
- [ ] CI time same or faster (parallel execution)
- [ ] Documentation updated

---

## 📊 Impact Analysis

### Before
- **1 file**: 1,234 lines
- **Navigation**: Difficult (scroll through entire file)
- **CI**: Sequential execution
- **Conflicts**: High risk
- **Maintenance**: Time-consuming

### After
- **8 files**: Average 150 lines each
- **Navigation**: Easy (descriptive filenames)
- **CI**: Parallel execution (Jest default)
- **Conflicts**: Low risk (different files)
- **Maintenance**: Quick (focused files)

### Benefits
| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| File size | 1,234 lines | ~150 lines avg | 88% reduction |
| Navigation time | 30-60s scroll | 5s file selection | 83% faster |
| Merge conflicts | High | Low | -70% |
| Code review | Overwhelming | Manageable | +50% quality |
| CI parallelization | No | Yes | -20% time |

---

## 🔗 Related Issues

- [TEST-004](./frontend-sse-mock-helper.md) - SSE Mock Helper (prerequisite)
- [TEST-005](./frontend-replace-global-mocks.md) - Replace Global Mocks
- [TEST-011](./frontend-performance-tests.md) - Performance Tests

---

## 📚 References

- [Jest Test Structure](https://jestjs.io/docs/setup-teardown)
- [Testing Library Best Practices](https://kentcdodds.com/blog/common-mistakes-with-react-testing-library)
- [Test File Organization](https://testingjavascript.com/)

---

## 📈 Effort Estimate

**Total: 4-6 hours**

| Phase | Effort | Description |
|-------|--------|-------------|
| Setup directory & helpers | 1h | Create structure, shared utilities |
| Split into 8 files | 2-3h | Copy/paste, organize by concern |
| Verify & cleanup | 1-2h | Run tests, remove duplication |

---

## 🧪 Testing Strategy

### Validation Steps
1. **Each file individually**:
   ```bash
   pnpm test useChatStreaming/initial-state.test.ts
   pnpm test useChatStreaming/event-handling.test.ts
   # ... all files
   ```

2. **All files together**:
   ```bash
   pnpm test useChatStreaming/
   ```

3. **Coverage comparison**:
   ```bash
   # Before split
   pnpm test:coverage useChatStreaming.test.ts

   # After split
   pnpm test:coverage useChatStreaming/

   # Coverage should be identical
   ```

4. **CI run**:
   ```bash
   # Full test suite should pass
   pnpm test
   ```

### Success Metrics
- [ ] All tests pass (100% migration)
- [ ] Coverage maintained or improved
- [ ] CI time reduced or same
- [ ] 0 test failures introduced

---

**Last Updated**: 2025-11-20
**Status**: 🔴 Open - Ready for Implementation
**Assignee**: TBD
**Depends On**: TEST-004 (SSE Mock Helper)
