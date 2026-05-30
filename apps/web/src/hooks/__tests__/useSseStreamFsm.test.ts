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
});
