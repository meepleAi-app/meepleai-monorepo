# Performance Testing Guide (Issue #1509)

**Purpose**: Measure and prevent performance regressions in critical frontend components.

## Table of Contents

1. [Overview](#overview)
2. [Performance Test Infrastructure](#performance-test-infrastructure)
3. [Running Performance Tests](#running-performance-tests)
4. [Writing Performance Tests](#writing-performance-tests)
5. [Performance Thresholds](#performance-thresholds)
6. [Best Practices](#best-practices)
7. [CI Integration](#ci-integration)
8. [Troubleshooting](#troubleshooting)

---

## Overview

Performance tests measure three key metrics:
- **Render Time**: Time to initially render a component (ms)
- **Re-render Count**: Number of renders triggered by state changes
- **Memory Usage**: Heap memory increase (MB)

**Technology Stack**:
- **Test Runner**: Vitest (migrated from Jest in Issue #1503)
- **Testing Library**: @testing-library/react v16+ (React 19 compatible)
- **Performance API**: browser `performance.now()` and `performance.memory`

---

## Performance Test Infrastructure

### Core Utilities

Located at: `src/test-utils/performance-test-utils.ts`

#### `measureRenderPerformance()`
Measures component render performance.

```typescript
const result = await measureRenderPerformance(() => {
  const { unmount } = render(<MyComponent />);
  unmount(); // Always unmount after measurement
});

expect(result.renderTime).toBeLessThan(100);
console.log(`Render time: ${result.renderTime.toFixed(2)}ms`);
console.log(`Memory increase: ${result.memoryIncrease.toFixed(2)}MB`);
```

#### `measureHookPerformance()`
Measures custom hook performance.

```typescript
const { result, performance } = await measureHookPerformance(() =>
  useMyCustomHook({ config: { ... } })
);

expect(performance.renderTime).toBeLessThan(50);
```

#### `runPerformanceTest()`
Runs multiple iterations and returns median (reduces GC variability).

```typescript
const medianResult = await runPerformanceTest(() => {
  const { unmount } = render(<MyComponent />);
  unmount();
}, 5); // 5 iterations

expect(medianResult.renderTime).toBeLessThan(150);
```

#### `assertPerformanceThresholds()`
Validates against predefined thresholds.

```typescript
assertPerformanceThresholds(
  result,
  DEFAULT_THRESHOLDS.medium,
  'MyComponent'
);
```

---

## Running Performance Tests

### Local Development

```bash
# Run all tests (includes performance tests)
pnpm test

# Run only performance tests
pnpm test -- -t "Performance"

# Run specific performance test file
pnpm test VirtualizedMessageList.performance.test.tsx

# Run with UI for detailed metrics
pnpm test:ui
```

### CI/CD

Performance tests run automatically in CI:
- Tests fail if thresholds are exceeded
- Performance metrics logged for monitoring
- Median measurements reduce flakiness

---

## Writing Performance Tests

### Basic Performance Test

```typescript
import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import { MyComponent } from '../MyComponent';
import {
  measureRenderPerformance,
  DEFAULT_THRESHOLDS,
  assertPerformanceThresholds,
} from '@/test-utils/performance-test-utils';

describe('MyComponent Performance', () => {
  beforeEach(() => {
    // Setup
  });

  it('should render 100 items within 500ms', async () => {
    const items = generateMockItems(100);

    const result = await measureRenderPerformance(() => {
      const { unmount } = render(<MyComponent items={items} />);
      unmount(); // Important: cleanup
    });

    expect(result.renderTime).toBeLessThan(500);

    // Log for CI monitoring
    console.log(`[PERF] 100 items: ${result.renderTime.toFixed(2)}ms`);
    console.log(`[PERF] Memory: ${result.memoryIncrease.toFixed(2)}MB`);
  });
});
```

### Hook Performance Test

```typescript
it('should initialize hook within 50ms', async () => {
  const { performance } = await measureHookPerformance(() =>
    useMyHook({ option: 'value' })
  );

  expect(performance.renderTime).toBeLessThan(50);

  assertPerformanceThresholds(
    performance,
    DEFAULT_THRESHOLDS.lightweight,
    'useMyHook initialization'
  );
});
```

### State Update Performance Test

```typescript
it('should update state efficiently', async () => {
  const store = createMyStore();

  const startTime = performance.now();

  act(() => {
    store.setState({ items: generateItems(100) });
  });

  const endTime = performance.now();
  const duration = endTime - startTime;

  expect(duration).toBeLessThan(100);
  console.log(`[PERF] State update: ${duration.toFixed(2)}ms`);
});
```

### Median Performance Test (Reduces Flakiness)

```typescript
it('should have consistent performance across runs', async () => {
  const items = generateMockItems(100);

  const medianResult = await runPerformanceTest(() => {
    const { unmount } = render(<MyComponent items={items} />);
    unmount();
  }, 5); // 5 iterations

  expect(medianResult.renderTime).toBeLessThan(600);

  console.log(`[PERF] Median (5 runs): ${medianResult.renderTime.toFixed(2)}ms`);
});
```

---

## Performance Thresholds

### Predefined Thresholds

Located in `performance-test-utils.ts`:

```typescript
export const DEFAULT_THRESHOLDS = {
  lightweight: {
    maxRenderTime: 50,         // Buttons, inputs
    maxRerenders: 3,
    maxMemoryIncreaseMB: 2,
  },
  medium: {
    maxRenderTime: 100,        // Forms, small lists (<20 items)
    maxRerenders: 5,
    maxMemoryIncreaseMB: 5,
  },
  heavy: {
    maxRenderTime: 500,        // Virtualized lists, complex state
    maxRerenders: 10,
    maxMemoryIncreaseMB: 10,
  },
  veryHeavy: {
    maxRenderTime: 1000,       // Large datasets (100+ items)
    maxRerenders: 15,
    maxMemoryIncreaseMB: 20,
  },
};
```

### Component Classification

| Component Type | Threshold Category | Examples |
|----------------|-------------------|----------|
| Simple UI | `lightweight` | Button, Input, Label, Badge |
| Form Components | `medium` | SearchFilters, LoginForm |
| List Components | `heavy` | MessageList (50-100 items) |
| Virtualized Lists | `veryHeavy` | VirtualizedMessageList (100-1000 items) |
| Hooks (Initial) | `lightweight` | useStreamingChat (initialization) |
| Hooks (Active) | `medium` | useStreamingChat (streaming) |
| State Stores | `lightweight` | Zustand store updates |

### Custom Thresholds

```typescript
const customThresholds = {
  maxRenderTime: 300,
  maxRerenders: 8,
  maxMemoryIncreaseMB: 15,
};

assertPerformanceThresholds(result, customThresholds, 'MyComponent');
```

---

## Best Practices

### 1. Always Unmount After Measurement

```typescript
// ✅ GOOD
const result = await measureRenderPerformance(() => {
  const { unmount } = render(<MyComponent />);
  unmount(); // Cleanup
});

// ❌ BAD (memory leak)
const result = await measureRenderPerformance(() => {
  render(<MyComponent />);
});
```

### 2. Use Median for Consistency

```typescript
// ✅ GOOD (reduces GC variability)
const medianResult = await runPerformanceTest(renderFn, 5);

// ⚠️ OK (may be flaky)
const result = await measureRenderPerformance(renderFn);
```

### 3. Log Metrics for CI Monitoring

```typescript
console.log(`[PERF] Component: ${result.renderTime.toFixed(2)}ms`);
console.log(`[PERF] Memory: ${result.memoryIncrease.toFixed(2)}MB`);
```

### 4. Test Realistic Data Volumes

```typescript
// ✅ GOOD (realistic scenario)
const messages = generateMockMessages(100); // Typical chat history

// ❌ BAD (unrealistic)
const messages = generateMockMessages(5);
```

### 5. Test Performance Variants

```typescript
describe('MyComponent Performance', () => {
  it('should render 50 items efficiently', async () => { ... });
  it('should render 100 items efficiently', async () => { ... });
  it('should render 500 items efficiently', async () => { ... });
});
```

### 6. Use Automated Thresholds

```typescript
// ✅ GOOD (automated validation)
assertPerformanceThresholds(
  result,
  DEFAULT_THRESHOLDS.heavy,
  'VirtualizedList'
);

// ⚠️ OK (manual assertion)
expect(result.renderTime).toBeLessThan(500);
```

---

## CI Integration

### Performance Test Configuration

Performance tests run in CI with the following setup:

**File**: `.github/workflows/ci.yml` (web job)

```yaml
- name: Run Tests
  run: pnpm test
  env:
    NODE_OPTIONS: --expose-gc  # Enable GC for memory tests
```

### Failure Handling

- **Threshold Exceeded**: Test fails, PR blocked
- **Flaky Tests**: Use `runPerformanceTest()` with 5+ iterations
- **Memory Tests**: Only run if `performance.memory` available (Chrome)

### Monitoring

Performance metrics are logged as `[PERF]` entries:

```
[PERF] VirtualizedMessageList (100 messages): 342.15ms
[PERF] Memory increase: 8.42MB
[PERF] Median render time (5 runs): 356.80ms
```

---

## Troubleshooting

### Issue: Flaky Performance Tests

**Symptom**: Tests pass locally but fail in CI

**Solution**:
```typescript
// Use median instead of single run
const medianResult = await runPerformanceTest(renderFn, 5);
```

### Issue: Memory Tests Always Return 0

**Symptom**: `result.memoryIncrease` is always 0

**Cause**: `performance.memory` not available (Firefox, Safari)

**Solution**: Memory assertions only run if API available
```typescript
if (result.memoryBefore > 0) {
  expect(result.memoryIncrease).toBeLessThan(20);
}
```

### Issue: Performance Degradation Over Time

**Symptom**: Tests start failing after code changes

**Solution**:
1. Review recent commits
2. Check for unoptimized re-renders (`React DevTools Profiler`)
3. Verify memoization (`useMemo`, `useCallback`)
4. Check for memory leaks (missing cleanup)

### Issue: Inconsistent Results

**Symptom**: Wide variance in render times

**Solution**:
- Run with median: `runPerformanceTest(renderFn, 5)`
- Increase iteration count for stability
- Check for background processes during test

---

## Real-World Examples

### VirtualizedMessageList (100 messages)

```typescript
it('should render 100 messages within 500ms', async () => {
  const messages = generateMockMessages(100);

  const result = await measureRenderPerformance(() => {
    const { unmount } = render(
      <VirtualizedMessageList
        messages={messages}
        userAvatar={{ fallback: 'U' }}
      />
    );
    unmount();
  });

  expect(result.renderTime).toBeLessThan(500);
  console.log(`[PERF] 100 messages: ${result.renderTime.toFixed(2)}ms`);
});
```

**Expected Result**: ~300-400ms

### useStreamingChat (50 token updates)

```typescript
it('should handle 50 token updates within 500ms', async () => {
  const tokens = Array.from({ length: 50 }, (_, i) => `token${i} `);

  (global.fetch as ReturnType<typeof vi.fn>).mockImplementation(() =>
    Promise.resolve(createMockStreamResponse(tokens))
  );

  const startTime = performance.now();

  const { result } = renderHook(() =>
    useStreamingChat({ onComplete: vi.fn() })
  );

  await act(async () => {
    await result.current[1].startStreaming('game-id', 'question');
  });

  await waitFor(() => expect(result.current[0].isStreaming).toBe(false));

  const endTime = performance.now();
  const duration = endTime - startTime;

  expect(duration).toBeLessThan(500);
  console.log(`[PERF] 50 token updates: ${duration.toFixed(2)}ms`);
});
```

**Expected Result**: ~200-300ms

### Chat Store (100 thread update)

```typescript
it('should update 100 threads within 100ms', async () => {
  const store = createChatStore();
  const threads = generateMockThreads('game-1', 100);

  const startTime = performance.now();

  act(() => {
    store.setState((state) => {
      state.chatsByGame['game-1'] = threads;
    });
  });

  const endTime = performance.now();
  const duration = endTime - startTime;

  expect(duration).toBeLessThan(100);
  console.log(`[PERF] 100 thread update: ${duration.toFixed(2)}ms`);
});
```

**Expected Result**: ~30-60ms

---

## Related Issues

- **#1509**: Add Performance Tests (frontend) - *This document*
- **#1503**: Migrate Jest to Vitest (completed 2025-11-24)
- **#1496**: Visual Regression Tests (Chromatic)
- **#1098**: Comprehensive Component Tests

---

## Resources

- [Vitest Performance](https://vitest.dev/guide/features.html#benchmarking)
- [React Testing Library Best Practices](https://testing-library.com/docs/react-testing-library/intro/)
- [Web Performance APIs](https://developer.mozilla.org/en-US/docs/Web/API/Performance)
- [React Profiler](https://react.dev/reference/react/Profiler)

---

**Last Updated**: 2025-12-05
**Author**: Engineering Team
**Issue**: #1509
