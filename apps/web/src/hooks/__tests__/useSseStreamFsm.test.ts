import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useSseStreamFsm } from '../useSseStreamFsm';

interface TestEvent {
  type: 'token' | 'done';
  value?: string;
}
interface TestState {
  tokens: string[];
  done: boolean;
}

function asyncGenOf<T>(items: T[]): AsyncGenerator<T> {
  return (async function* () {
    for (const it of items) yield it;
  })();
}

describe('useSseStreamFsm — base FSM skeleton', () => {
  beforeEach(() => {
    vi.useRealTimers();
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('initializes with initialState, retryCount=0, error=null', () => {
    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, Error>({
        transport: () => asyncGenOf<TestEvent>([]),
        initialState: { tokens: [], done: false },
        eventReducer: (s, e) =>
          e.type === 'token' ? { ...s, tokens: [...s.tokens, e.value!] } : { ...s, done: true },
        errorMapper: err => (err instanceof Error ? err : new Error(String(err))),
        retryPolicy: { maxRetries: 0, backoffMs: [], retryableErrorKinds: [] },
      })
    );

    expect(result.current.state).toEqual({ tokens: [], done: false });
    expect(result.current.retryCount).toBe(0);
    expect(result.current.error).toBeNull();
  });

  it('iterates transport events through eventReducer into state', async () => {
    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, Error>({
        transport: () =>
          asyncGenOf<TestEvent>([
            { type: 'token', value: 'hello' },
            { type: 'token', value: ' world' },
            { type: 'done' },
          ]),
        initialState: { tokens: [], done: false },
        eventReducer: (s, e) =>
          e.type === 'token' ? { ...s, tokens: [...s.tokens, e.value!] } : { ...s, done: true },
        errorMapper: err => (err instanceof Error ? err : new Error(String(err))),
        retryPolicy: { maxRetries: 0, backoffMs: [], retryableErrorKinds: [] },
      })
    );

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => {
      expect(result.current.state.done).toBe(true);
    });

    expect(result.current.state.tokens).toEqual(['hello', ' world']);
  });

  it('retries on retryable error with configured backoff schedule', async () => {
    vi.useFakeTimers();

    let attempt = 0;
    const transport = vi.fn(() => {
      attempt++;
      if (attempt < 3) {
        // Synchronously throw inside the generator
        return (async function* (): AsyncGenerator<TestEvent> {
          throw new TypeError('Failed to fetch');
        })();
      }
      return asyncGenOf<TestEvent>([{ type: 'done' }]);
    });

    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, { kind: string; message: string }>({
        transport,
        initialState: { tokens: [], done: false },
        eventReducer: (s, e) => (e.type === 'done' ? { ...s, done: true } : s),
        errorMapper: err =>
          err instanceof TypeError
            ? { kind: 'connection', message: err.message }
            : { kind: 'server', message: String(err) },
        retryPolicy: {
          maxRetries: 3,
          backoffMs: [1000, 3000, 9000],
          retryableErrorKinds: ['connection'],
        },
      })
    );

    act(() => {
      result.current.ask('q');
    });
    await vi.runAllTimersAsync();

    // review fix Mi-2: tighten assertion. With maxRetries=3 and success on the 3rd call,
    // retryCount MUST be exactly 2 (two retries scheduled). Loose >=2 would mask off-by-one.
    expect(result.current.retryCount).toBe(2);
    expect(transport).toHaveBeenCalledTimes(3);
    expect(result.current.state.done).toBe(true);
    expect(result.current.error).toBeNull();

    vi.useRealTimers();
  });

  it('does NOT retry on non-retryable error kind', async () => {
    vi.useFakeTimers();

    let attempt = 0;
    const transport = vi.fn(() => {
      attempt++;
      return (async function* (): AsyncGenerator<TestEvent> {
        throw new Error('server boom');
      })();
    });

    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, { kind: string; message: string }>({
        transport,
        initialState: { tokens: [], done: false },
        eventReducer: s => s,
        errorMapper: err => ({ kind: 'server', message: String(err) }),
        retryPolicy: {
          maxRetries: 3,
          backoffMs: [1000, 3000, 9000],
          retryableErrorKinds: ['connection'], // 'server' is NOT in this list
        },
      })
    );

    act(() => {
      result.current.ask('q');
    });
    await vi.runAllTimersAsync();

    expect(transport).toHaveBeenCalledTimes(1);
    expect(result.current.retryCount).toBe(0);
    expect(result.current.error).toEqual({ kind: 'server', message: 'Error: server boom' });

    vi.useRealTimers();
  });

  it('stops retrying after maxRetries and surfaces final error', async () => {
    vi.useFakeTimers();

    const transport = vi.fn(() =>
      (async function* (): AsyncGenerator<TestEvent> {
        throw new TypeError('Failed to fetch');
      })()
    );

    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, { kind: string; message: string }>({
        transport,
        initialState: { tokens: [], done: false },
        eventReducer: s => s,
        errorMapper: err => ({ kind: 'connection', message: String(err) }),
        retryPolicy: {
          maxRetries: 2,
          backoffMs: [100, 200],
          retryableErrorKinds: ['connection'],
        },
      })
    );

    act(() => {
      result.current.ask('q');
    });
    await vi.runAllTimersAsync();

    // initial + 2 retries = 3 attempts
    expect(transport).toHaveBeenCalledTimes(3);
    expect(result.current.error).toEqual({
      kind: 'connection',
      message: 'TypeError: Failed to fetch',
    });

    vi.useRealTimers();
  });
});
