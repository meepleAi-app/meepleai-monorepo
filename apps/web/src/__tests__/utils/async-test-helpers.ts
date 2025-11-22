import { act, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

/**
 * Advance fake timers and flush pending promises
 * Use this when testing components with timers/intervals
 *
 * @example
 * await advanceTimersAndFlush(2000); // Advance 2 seconds and flush promises
 */
export async function advanceTimersAndFlush(ms: number): Promise<void> {
  await act(async () => {
    jest.advanceTimersByTime(ms);
    await Promise.resolve(); // Flush microtask queue
  });
}

/**
 * Wait for async effects to complete
 * Use after render/user interactions with async side effects
 *
 * @example
 * render(<MyComponent />);
 * await waitForAsyncEffects(); // Let async effects settle
 */
export async function waitForAsyncEffects(): Promise<void> {
  await act(async () => {
    await Promise.resolve();
  });
}

/**
 * Setup userEvent for testing
 * Automatically handles act() internally
 *
 * @example
 * const user = setupUserEvent();
 * await user.click(button);
 */
export function setupUserEvent() {
  return userEvent.setup({ delay: null });
}

/**
 * Flush all pending timers and promises
 * Use in afterEach to ensure clean test state
 *
 * @example
 * afterEach(async () => {
 *   await flushAllPending();
 *   jest.useRealTimers();
 * });
 */
export async function flushAllPending(): Promise<void> {
  await act(async () => {
    jest.runOnlyPendingTimers();
    await Promise.resolve();
  });
}

/**
 * Wait for a condition with custom timeout
 * Wraps waitFor with common options
 *
 * @example
 * await waitForCondition(() => {
 *   expect(screen.getByText('Loaded')).toBeInTheDocument();
 * }, 5000);
 */
export async function waitForCondition(
  callback: () => void,
  timeout: number = 3000
): Promise<void> {
  await waitFor(callback, { timeout });
}
