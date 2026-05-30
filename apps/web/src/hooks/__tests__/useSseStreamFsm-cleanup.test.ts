import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSseStreamFsm } from '../useSseStreamFsm';

interface TestEvent {
  type: 'token' | 'done';
  value?: string;
}
interface TestState {
  done: boolean;
}

describe('useSseStreamFsm — cleanup', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('clears all timers and aborts controller on unmount during streaming', async () => {
    const abortHandler = vi.fn();
    const slowTransport = vi.fn((_: string, signal: AbortSignal) => {
      signal.addEventListener('abort', abortHandler);
      return (async function* (): AsyncGenerator<TestEvent> {
        // Yield once to arm watchdog, then hang forever
        yield { type: 'token', value: 'a' };
        await new Promise(() => {});
      })();
    });

    const { result, unmount } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, Error>({
        transport: slowTransport,
        initialState: { done: false },
        eventReducer: (s, e) => (e.type === 'done' ? { ...s, done: true } : s),
        errorMapper: err => (err instanceof Error ? err : new Error(String(err))),
        retryPolicy: { maxRetries: 0, backoffMs: [], timeoutMs: 30_000, retryableErrorKinds: [] },
      })
    );

    act(() => {
      result.current.ask('q');
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(10);
    });

    // Sanity: watchdog timer is pending (armed by the yielded 'a' event)
    expect(vi.getTimerCount()).toBeGreaterThan(0);

    unmount();

    expect(vi.getTimerCount()).toBe(0);
    expect(abortHandler).toHaveBeenCalledTimes(1);
  });

  it('clears retry timer on unmount during backoff window', async () => {
    let attempt = 0;
    const transport = vi.fn(() => {
      attempt++;
      return (async function* (): AsyncGenerator<TestEvent> {
        throw new TypeError('boom');
      })();
    });

    const { result, unmount } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, { kind: string }>({
        transport,
        initialState: { done: false },
        eventReducer: s => s,
        errorMapper: () => ({ kind: 'connection' }),
        retryPolicy: {
          maxRetries: 3,
          backoffMs: [5_000, 5_000, 5_000],
          retryableErrorKinds: ['connection'],
        },
      })
    );

    act(() => {
      result.current.ask('q');
    });
    // First attempt errored; retry timer should be pending
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(vi.getTimerCount()).toBeGreaterThan(0);
    expect(transport).toHaveBeenCalledTimes(1);

    unmount();

    expect(vi.getTimerCount()).toBe(0);
    // Advance well past backoff — transport must NOT be called again
    await vi.advanceTimersByTimeAsync(30_000);
    expect(transport).toHaveBeenCalledTimes(1);
  });
});
