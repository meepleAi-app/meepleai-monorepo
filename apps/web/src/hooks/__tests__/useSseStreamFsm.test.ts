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

  it('fires watchdog timeout when no event arrives within timeoutMs', async () => {
    vi.useFakeTimers();

    const slowTransport = vi.fn((_input: string, signal: AbortSignal) =>
      // Yield immediately to arm watchdog, then hang forever
      (async function* (): AsyncGenerator<TestEvent> {
        yield { type: 'token', value: '' }; // Triggers watchdog arm
        // Now hang forever, respecting abort signal
        await new Promise((_, reject) => {
          signal.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
        });
        yield { type: 'done' };
      })()
    );

    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, { kind: string; message: string }>({
        transport: slowTransport,
        initialState: { tokens: [], done: false },
        eventReducer: s => s,
        errorMapper: err => {
          if ((err as { __sseStreamFsmReason?: string }).__sseStreamFsmReason === 'timeout') {
            return { kind: 'timeout', message: 'no response in 30s' };
          }
          return { kind: 'connection', message: String(err) };
        },
        retryPolicy: {
          maxRetries: 0,
          backoffMs: [],
          timeoutMs: 30_000,
          retryableErrorKinds: [],
        },
      })
    );

    act(() => {
      result.current.ask('q');
    });
    // Run all timers to allow the watchdog to fire
    await act(async () => {
      await vi.runAllTimersAsync();
    });

    // The error should be set to timeout kind
    expect(result.current.error?.kind).toBe('timeout');

    vi.useRealTimers();
  });

  it('does NOT schedule watchdog when timeoutMs is undefined', async () => {
    vi.useFakeTimers();

    const slowTransport = vi.fn(() =>
      (async function* (): AsyncGenerator<TestEvent> {
        await new Promise(() => {});
        yield { type: 'done' };
      })()
    );

    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, Error>({
        transport: slowTransport,
        initialState: { tokens: [], done: false },
        eventReducer: s => s,
        errorMapper: err => (err instanceof Error ? err : new Error(String(err))),
        retryPolicy: { maxRetries: 0, backoffMs: [], retryableErrorKinds: [] },
        // timeoutMs intentionally omitted
      })
    );

    act(() => {
      result.current.ask('q');
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(120_000);
    });

    expect(result.current.error).toBeNull(); // never timed out

    vi.useRealTimers();
  });
});

/**
 * #4335: uno stream che si chiude in modo pulito senza l'evento che lo conclude. Il backend, o un
 * proxy, chiude la connessione a metà: il trasporto esce dal ciclo di lettura e l'automa finiva il
 * suo `for await` senza toccare niente, lasciando la vista «in corso» per sempre.
 *
 * Il passo è su richiesta (`isTerminalEvent`): solo chi sa quale evento chiude una risposta può dire
 * che una risposta non è stata chiusa.
 */
describe('useSseStreamFsm — a stream that ends without its terminal event (#4335)', () => {
  type Mapped = { kind: string; message: string };

  const mapper = (err: unknown): Mapped =>
    (err as { __sseStreamFsmReason?: string } | null)?.__sseStreamFsmReason === 'interrupted'
      ? { kind: 'interrupted', message: 'interrotta' }
      : { kind: 'server', message: String(err) };

  const reducer = (s: TestState, e: TestEvent): TestState =>
    e.type === 'token' ? { ...s, tokens: [...s.tokens, e.value!] } : { ...s, done: true };

  it('hands an interruption to errorMapper when the stream ends before the terminal event', async () => {
    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, Mapped>({
        transport: () => asyncGenOf<TestEvent>([{ type: 'token', value: 'Cinque ' }]),
        initialState: { tokens: [], done: false },
        eventReducer: reducer,
        errorMapper: mapper,
        isTerminalEvent: e => e.type === 'done',
        retryPolicy: { maxRetries: 0, backoffMs: [], retryableErrorKinds: [] },
      })
    );

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => {
      expect(result.current.error).toEqual({ kind: 'interrupted', message: 'interrotta' });
    });
    // Ciò che è arrivato prima della chiusura resta nello stato: è il consumatore a decidere.
    expect(result.current.state.tokens).toEqual(['Cinque ']);
  });

  it('leaves the error alone when the stream ends after the terminal event', async () => {
    const errorMapper = vi.fn(mapper);
    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, Mapped>({
        transport: () =>
          asyncGenOf<TestEvent>([{ type: 'token', value: 'Cinque' }, { type: 'done' }]),
        initialState: { tokens: [], done: false },
        eventReducer: reducer,
        errorMapper,
        isTerminalEvent: e => e.type === 'done',
        retryPolicy: { maxRetries: 0, backoffMs: [], retryableErrorKinds: [] },
      })
    );

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => {
      expect(result.current.state.done).toBe(true);
    });
    // Lo stream si chiude dopo l'ultimo evento: lascia il tempo al passo finale di girare.
    await act(async () => {
      await new Promise(resolve => setTimeout(resolve, 10));
    });
    expect(result.current.error).toBeNull();
    expect(errorMapper).not.toHaveBeenCalled();
  });

  it('changes nothing for a consumer that does not name a terminal event', async () => {
    const errorMapper = vi.fn(mapper);
    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, Mapped>({
        transport: () => asyncGenOf<TestEvent>([{ type: 'token', value: 'Cinque ' }]),
        initialState: { tokens: [], done: false },
        eventReducer: reducer,
        errorMapper,
        retryPolicy: { maxRetries: 0, backoffMs: [], retryableErrorKinds: [] },
      })
    );

    act(() => {
      result.current.ask('q');
    });

    await waitFor(() => {
      expect(result.current.state.tokens).toEqual(['Cinque ']);
    });
    await act(async () => {
      await new Promise(resolve => setTimeout(resolve, 10));
    });
    expect(result.current.error).toBeNull();
    expect(errorMapper).not.toHaveBeenCalled();
  });

  it('does not call a stream stopped by the user an interruption', async () => {
    const errorMapper = vi.fn(mapper);
    let release: (() => void) | undefined;
    const transport = (_input: string, signal: AbortSignal): AsyncGenerator<TestEvent> =>
      (async function* () {
        yield { type: 'token', value: 'Cinque ' } as TestEvent;
        await new Promise<void>(resolve => {
          release = resolve;
        });
        // Fermato, il trasporto esce senza errore: come `agentChatTransport` sul segnale.
        if (signal.aborted) return;
      })();

    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, Mapped>({
        transport,
        initialState: { tokens: [], done: false },
        eventReducer: reducer,
        errorMapper,
        isTerminalEvent: e => e.type === 'done',
        retryPolicy: { maxRetries: 0, backoffMs: [], retryableErrorKinds: [] },
      })
    );

    act(() => {
      result.current.ask('q');
    });
    await waitFor(() => {
      expect(result.current.state.tokens).toEqual(['Cinque ']);
    });

    act(() => {
      result.current.stop();
    });
    await act(async () => {
      release?.();
      await new Promise(resolve => setTimeout(resolve, 10));
    });

    expect(result.current.error).toBeNull();
    expect(errorMapper).not.toHaveBeenCalled();
  });

  it('retries an interruption when the consumer maps it to a retryable kind', async () => {
    vi.useFakeTimers();
    let attempt = 0;
    const transport = vi.fn(() => {
      attempt++;
      return attempt === 1
        ? asyncGenOf<TestEvent>([])
        : asyncGenOf<TestEvent>([{ type: 'token', value: 'Cinque' }, { type: 'done' }]);
    });

    const { result } = renderHook(() =>
      useSseStreamFsm<string, TestEvent, TestState, Mapped>({
        transport,
        initialState: { tokens: [], done: false },
        eventReducer: reducer,
        errorMapper: mapper,
        isTerminalEvent: e => e.type === 'done',
        retryPolicy: { maxRetries: 1, backoffMs: [100], retryableErrorKinds: ['interrupted'] },
      })
    );

    act(() => {
      result.current.ask('q');
    });
    await vi.runAllTimersAsync();

    expect(transport).toHaveBeenCalledTimes(2);
    expect(result.current.retryCount).toBe(1);
    expect(result.current.state.done).toBe(true);
    expect(result.current.error).toBeNull();

    vi.useRealTimers();
  });
});
