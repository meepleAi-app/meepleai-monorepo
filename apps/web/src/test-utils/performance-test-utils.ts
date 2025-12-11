/**
 * Performance Testing Utilities (Issue #1509)
 *
 * Utilities for measuring frontend performance metrics:
 * - Render time
 * - Re-render count
 * - Memory usage
 *
 * Uses Vitest + @testing-library/react (React 19 compatible)
 */

import { renderHook, RenderHookResult } from '@testing-library/react';
import { ReactNode } from 'react';
import { logger } from '@/lib/logger';

// ============================================================================
// Types
// ============================================================================

export interface PerformanceThresholds {
  /** Maximum acceptable render time in ms */
  maxRenderTime: number;
  /** Maximum acceptable re-render count */
  maxRerenders: number;
  /** Maximum acceptable memory increase in MB */
  maxMemoryIncreaseMB: number;
}

export interface RenderPerformanceResult {
  /** Time taken to render in ms */
  renderTime: number;
  /** Number of renders that occurred */
  renderCount: number;
  /** Memory usage before test in MB */
  memoryBefore: number;
  /** Memory usage after test in MB */
  memoryAfter: number;
  /** Memory increase in MB */
  memoryIncrease: number;
}

// ============================================================================
// Constants
// ============================================================================

/** Default performance thresholds for different test types */
export const DEFAULT_THRESHOLDS = {
  /** Lightweight components (buttons, inputs) */
  lightweight: {
    maxRenderTime: 50,
    maxRerenders: 3,
    maxMemoryIncreaseMB: 2,
  },
  /** Medium components (forms, lists with <20 items) */
  medium: {
    maxRenderTime: 100,
    maxRerenders: 5,
    maxMemoryIncreaseMB: 5,
  },
  /** Heavy components (virtualized lists, complex state) */
  heavy: {
    maxRenderTime: 500,
    maxRerenders: 10,
    maxMemoryIncreaseMB: 10,
  },
  /** Very heavy components (large data sets, 100+ items) */
  veryHeavy: {
    maxRenderTime: 1000,
    maxRerenders: 15,
    maxMemoryIncreaseMB: 20,
  },
} as const;

// ============================================================================
// Memory Utilities
// ============================================================================

// Track if warning already logged to avoid spam
let memoryApiWarningLogged = false;

/**
 * Get current heap usage in MB
 * Note: Uses performance.memory (Chrome-only) or estimates
 */
function getHeapUsageMB(): number {
  if ('memory' in performance) {
    // Chrome provides performance.memory
    const memory = (performance as Performance & { memory?: { usedJSHeapSize: number } }).memory;
    if (!memory) {
      if (!memoryApiWarningLogged) {
        logger.warn('[PERF] performance.memory unavailable - memory tests will be skipped');
        memoryApiWarningLogged = true;
      }
      return 0;
    }
    return memory.usedJSHeapSize / 1024 / 1024;
  }
  if (!memoryApiWarningLogged) {
    logger.warn(
      '[PERF] performance.memory API not supported (non-Chrome browser) - memory tests will be skipped'
    );
    memoryApiWarningLogged = true;
  }
  return 0; // Fallback for non-Chrome browsers
}

/**
 * Force garbage collection if available (Node.js with --expose-gc)
 */
function forceGC() {
  if (global.gc) {
    global.gc();
  }
}

// ============================================================================
// Render Performance Measurement
// ============================================================================

/**
 * Measure component render performance
 *
 * @param renderFn - Function that renders the component
 * @param iterations - Number of iterations to run (default: 1)
 * @returns Performance metrics
 *
 * @example
 * ```ts
 * const result = await measureRenderPerformance(() => {
 *   render(<MyComponent />);
 * });
 * expect(result.renderTime).toBeLessThan(100);
 * ```
 */
export async function measureRenderPerformance(
  renderFn: () => void,
  iterations = 1
): Promise<RenderPerformanceResult> {
  forceGC();
  await new Promise(resolve => setTimeout(resolve, 100)); // Let GC settle

  const memoryBefore = getHeapUsageMB();
  const startTime = performance.now();

  // Run iterations
  for (let i = 0; i < iterations; i++) {
    renderFn();
  }

  const endTime = performance.now();
  const memoryAfter = getHeapUsageMB();

  return {
    renderTime: (endTime - startTime) / iterations, // Average time per iteration
    renderCount: iterations,
    memoryBefore,
    memoryAfter,
    memoryIncrease: Math.max(0, memoryAfter - memoryBefore),
  };
}

// ============================================================================
// Hook Performance Measurement
// ============================================================================

/**
 * Measure hook render performance with re-render tracking
 *
 * @param hook - Hook function to test
 * @param wrapper - Optional wrapper component (e.g., providers)
 * @returns Performance result and render hook result
 *
 * @example
 * ```ts
 * const { result, performance } = await measureHookPerformance(() =>
 *   useStreamingChat({ onComplete: vi.fn() })
 * );
 * expect(performance.renderTime).toBeLessThan(50);
 * ```
 */
export async function measureHookPerformance<TProps, TResult>(
  hook: (props: TProps) => TResult,
  options?: {
    wrapper?: React.ComponentType<{ children: ReactNode }>;
    initialProps?: TProps;
  }
): Promise<{
  result: RenderHookResult<TResult, TProps>;
  performance: RenderPerformanceResult;
}> {
  forceGC();
  await new Promise(resolve => setTimeout(resolve, 100));

  const memoryBefore = getHeapUsageMB();
  let renderCount = 0;

  // Track renders using renderHook
  const startTime = performance.now();
  const result = renderHook(hook, {
    wrapper: options?.wrapper,
    initialProps: options?.initialProps as TProps,
  });

  renderCount = 1; // Initial render

  const endTime = performance.now();
  const memoryAfter = getHeapUsageMB();

  return {
    result,
    performance: {
      renderTime: endTime - startTime,
      renderCount,
      memoryBefore,
      memoryAfter,
      memoryIncrease: Math.max(0, memoryAfter - memoryBefore),
    },
  };
}

// ============================================================================
// Re-render Counter
// ============================================================================

/**
 * Count re-renders for a component over a series of state updates
 *
 * @param renderFn - Function that renders and updates the component
 * @returns Number of renders that occurred
 *
 * @example
 * ```ts
 * const rerenderCount = await countRerenders((rerender) => {
 *   const { rerender: rerenderComponent } = render(<MyComponent count={0} />);
 *   rerenderComponent(<MyComponent count={1} />);
 *   rerenderComponent(<MyComponent count={2} />);
 * });
 * expect(rerenderCount).toBeLessThanOrEqual(3);
 * ```
 */
export async function countRerenders(renderFn: (rerender: () => void) => void): Promise<number> {
  let renderCount = 0;
  const rerender = () => {
    renderCount++;
  };

  renderFn(rerender);

  return renderCount;
}

// ============================================================================
// Performance Assertion Helpers
// ============================================================================

/**
 * Assert that performance metrics meet thresholds
 *
 * @param result - Performance result to validate
 * @param thresholds - Expected thresholds
 * @param testName - Name of test for error messages
 *
 * @example
 * ```ts
 * assertPerformanceThresholds(
 *   result,
 *   DEFAULT_THRESHOLDS.medium,
 *   'VirtualizedMessageList'
 * );
 * ```
 */
export function assertPerformanceThresholds(
  result: RenderPerformanceResult,
  thresholds: PerformanceThresholds,
  testName: string
) {
  // Render time assertion
  if (result.renderTime > thresholds.maxRenderTime) {
    throw new Error(
      `[${testName}] Render time ${result.renderTime.toFixed(2)}ms exceeds threshold ${thresholds.maxRenderTime}ms`
    );
  }

  // Re-render assertion
  if (result.renderCount > thresholds.maxRerenders) {
    throw new Error(
      `[${testName}] Render count ${result.renderCount} exceeds threshold ${thresholds.maxRerenders}`
    );
  }

  // Memory assertion (only if memory API available)
  if (result.memoryBefore > 0 && result.memoryIncrease > thresholds.maxMemoryIncreaseMB) {
    throw new Error(
      `[${testName}] Memory increase ${result.memoryIncrease.toFixed(2)}MB exceeds threshold ${thresholds.maxMemoryIncreaseMB}MB`
    );
  }
}

// ============================================================================
// Batch Performance Testing
// ============================================================================

/**
 * Run performance test with multiple iterations and return median result
 *
 * @param renderFn - Function to test
 * @param iterations - Number of test runs (default: 5)
 * @returns Median performance result
 *
 * @example
 * ```ts
 * const medianResult = await runPerformanceTest(
 *   () => render(<VirtualizedMessageList messages={messages} />),
 *   5
 * );
 * ```
 */
export async function runPerformanceTest(
  renderFn: () => void,
  iterations = 5
): Promise<RenderPerformanceResult> {
  const results: RenderPerformanceResult[] = [];

  for (let i = 0; i < iterations; i++) {
    const result = await measureRenderPerformance(renderFn);
    results.push(result);
    await new Promise(resolve => setTimeout(resolve, 200)); // Delay between runs
  }

  // Calculate median render time (proper median for even/odd arrays)
  const renderTimes = results.map(r => r.renderTime).sort((a, b) => a - b);
  const mid = Math.floor(renderTimes.length / 2);
  const medianRenderTime =
    renderTimes.length % 2 === 0 ? (renderTimes[mid - 1] + renderTimes[mid]) / 2 : renderTimes[mid];

  // Calculate median memory increase
  const memoryIncreases = results.map(r => r.memoryIncrease).sort((a, b) => a - b);
  const medianMemoryIncrease =
    memoryIncreases.length % 2 === 0
      ? (memoryIncreases[mid - 1] + memoryIncreases[mid]) / 2
      : memoryIncreases[mid];

  return {
    renderTime: medianRenderTime,
    renderCount: results[0].renderCount,
    memoryBefore: results[0].memoryBefore,
    memoryAfter: results[results.length - 1].memoryAfter,
    memoryIncrease: medianMemoryIncrease,
  };
}
