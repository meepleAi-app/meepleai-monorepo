/**
 * Safe fake timer utilities for Jest tests.
 *
 * Provides helper functions for correctly manipulating Jest fake timers in tests,
 * following React Testing Library best practices.
 *
 * @module test-utils/timer-test-helpers
 *
 * @warning CRITICAL: Never call timer functions inside waitFor() callbacks!
 *
 * ❌ BAD (Anti-pattern):
 * ```typescript
 * await waitFor(() => {
 *   jest.advanceTimersByTime(5000); // ❌ WRONG - waitFor() polls this callback repeatedly
 *   expect(element).not.toBeInTheDocument();
 * });
 * ```
 *
 * ✅ GOOD (Correct pattern):
 * ```typescript
 * jest.advanceTimersByTime(5000); // ✅ Advance timers BEFORE waitFor
 * await waitFor(() => {
 *   expect(element).not.toBeInTheDocument();
 * });
 * ```
 *
 * WHY? waitFor() polls the callback repeatedly until it passes or times out.
 * If you advance timers inside the callback, timers don't actually advance between polls,
 * causing the test to timeout.
 */

import { waitFor } from '@testing-library/react';

/**
 * Advances fake timers by the specified milliseconds, then waits for an assertion to pass.
 *
 * This is a convenience wrapper that ensures correct ordering:
 * 1. Advance timers first
 * 2. Then wait for assertion
 *
 * Use this instead of manually calling jest.advanceTimersByTime() to avoid anti-patterns.
 *
 * @param ms - Milliseconds to advance timers
 * @param assertion - Assertion function to wait for (must not throw when condition is met)
 *
 * @example
 * // Test auto-dismissing toast after 5 seconds
 * it('dismisses toast after timeout', async () => {
 *   jest.useFakeTimers();
 *   render(<ToastComponent />);
 *
 *   // Toast appears
 *   expect(screen.getByText('Success!')).toBeInTheDocument();
 *
 *   // Advance timers and wait for toast to disappear
 *   await advanceTimersAndWaitFor(5000, () => {
 *     expect(screen.queryByText('Success!')).not.toBeInTheDocument();
 *   });
 *
 *   jest.useRealTimers();
 * });
 */
export async function advanceTimersAndWaitFor(
  ms: number,
  assertion: () => void | Promise<void>
): Promise<void> {
  // Advance timers FIRST (critical: do NOT do this inside waitFor callback)
  jest.advanceTimersByTime(ms);

  // THEN wait for assertion to pass
  return waitFor(assertion);
}

/**
 * Sets up fake timers in beforeEach and restores real timers in afterEach.
 *
 * Call this at the top of a describe block to automatically manage timer lifecycle
 * for all tests in that block.
 *
 * Ensures:
 * - Fake timers are enabled before each test
 * - Pending timers are run after each test (prevents leaks)
 * - Real timers are restored after each test (prevents interference)
 *
 * @example
 * describe('Component with timers', () => {
 *   setupFakeTimers(); // Call once at top of describe block
 *
 *   it('does something after delay', async () => {
 *     render(<Component />);
 *     jest.advanceTimersByTime(1000);
 *     await waitFor(() => expect(...).toBeInTheDocument());
 *     // No need to manually clean up timers - handled by setupFakeTimers()
 *   });
 * });
 */
export function setupFakeTimers() {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    // Run any pending timers to completion (prevents leaks into next test)
    jest.runOnlyPendingTimers();

    // Restore real timers (prevents interference with other tests)
    jest.useRealTimers();
  });
}

/**
 * Advances timers by the specified milliseconds.
 *
 * This is a simple wrapper around jest.advanceTimersByTime() for consistency.
 * Included to provide a single import point for all timer utilities.
 *
 * @param ms - Milliseconds to advance
 *
 * @example
 * advanceTimers(5000); // Advance by 5 seconds
 * await waitFor(() => expect(...).not.toBeInTheDocument());
 */
export function advanceTimers(ms: number): void {
  jest.advanceTimersByTime(ms);
}

/**
 * Runs all pending timers to completion.
 *
 * Useful when you need to ensure all queued timer callbacks have executed,
 * but don't know the exact timing.
 *
 * @example
 * render(<Component />);
 * runAllPendingTimers(); // Execute all setTimeout/setInterval callbacks
 * expect(screen.getByText('Done')).toBeInTheDocument();
 */
export function runAllPendingTimers(): void {
  jest.runAllTimers();
}

/**
 * Configuration options for setupFakeTimers lifecycle management.
 *
 * Future: Could extend this to support custom timer configurations.
 * Currently defined for documentation purposes - implementation planned for future enhancement.
 */
export interface TimerSetupOptions {
  /**
   * Whether to run pending timers in afterEach cleanup.
   *
   * @default true
   * @remarks Recommended to prevent timer leaks into subsequent tests
   */
  runPendingAfterEach?: boolean;

  /**
   * Whether to restore real timers in afterEach cleanup.
   *
   * @default true
   * @remarks Recommended to prevent interference with other tests
   */
  restoreRealTimersAfterEach?: boolean;
}
