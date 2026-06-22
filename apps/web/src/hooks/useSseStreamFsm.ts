import { useCallback, useEffect, useRef, useState } from 'react';

import { flushSync } from 'react-dom';

export interface SseStreamFsmConfig<TInput, TEvent, TState, TError> {
  /**
   * Async generator producing parsed SSE events. Owner of fetch + parse + Zod (if any).
   * MUST throw a TypeError on network/connection error to trigger retry.
   * MUST honor the passed AbortSignal (call response.body?.cancel() / reader.releaseLock() on abort).
   */
  transport: (input: TInput, signal: AbortSignal) => AsyncGenerator<TEvent>;
  initialState: TState;
  /** Pure reducer: applies one event to state. Called inside the streaming loop. */
  eventReducer: (state: TState, event: TEvent) => TState;
  /** Maps raw thrown errors (or special "error" events) to consumer's TError shape. */
  errorMapper: (err: unknown) => TError;
  retryPolicy: {
    maxRetries: number;
    /** Backoff schedule. Must have length >= maxRetries. Example: [1000, 3000, 9000] for maxRetries=3. */
    backoffMs: readonly number[];
    /** When defined, lazy watchdog timer fires if no event arrives within timeoutMs (resets per event). */
    timeoutMs?: number;
    /**
     * String-kind whitelist. The errorMapper output is checked by reading the error's `kind`
     * property — if it matches one of these, the FSM schedules a retry.
     */
    retryableErrorKinds: readonly string[];
  };
}

export interface UseSseStreamFsmReturn<TInput, TState, TError> {
  state: TState;
  retryCount: number;
  error: TError | null;
  ask: (input: TInput) => void;
  stop: () => void;
  reset: () => void;
}

export function useSseStreamFsm<TInput, TEvent, TState, TError>(
  config: SseStreamFsmConfig<TInput, TEvent, TState, TError>
): UseSseStreamFsmReturn<TInput, TState, TError> {
  const [state, setState] = useState<TState>(config.initialState);
  const [retryCount, setRetryCount] = useState(0);
  const [error, setError] = useState<TError | null>(null);

  const abortRef = useRef<AbortController | null>(null);
  const watchdogRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const retryTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clearTimers = useCallback(() => {
    if (watchdogRef.current) {
      clearTimeout(watchdogRef.current);
      watchdogRef.current = null;
    }
    if (retryTimerRef.current) {
      clearTimeout(retryTimerRef.current);
      retryTimerRef.current = null;
    }
  }, []);

  const cleanup = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
    clearTimers();
  }, [clearTimers]);

  useEffect(() => cleanup, [cleanup]);

  const runStream = useCallback(
    async (input: TInput, attempt: number) => {
      const controller = new AbortController();
      abortRef.current = controller;
      let watchdogStarted = false;
      const armWatchdog = () => {
        if (config.retryPolicy.timeoutMs == null) return;
        if (watchdogRef.current) clearTimeout(watchdogRef.current);
        watchdogRef.current = setTimeout(() => {
          controller.abort();
          // Review fix B-1: plain setState (no flushSync) — watchdog fires outside any render cycle.
          // flushSync inside a timer callback is unsafe in React 19 concurrent mode.
          setError(config.errorMapper({ __sseStreamFsmReason: 'timeout' }));
        }, config.retryPolicy.timeoutMs);
      };

      try {
        for await (const event of config.transport(input, controller.signal)) {
          if (controller.signal.aborted) return;
          if (!watchdogStarted) {
            watchdogStarted = true;
            armWatchdog();
          } else armWatchdog();
          flushSync(() => {
            setState(s => config.eventReducer(s, event));
          });
        }
      } catch (rawErr) {
        if (controller.signal.aborted) return;
        const mapped = config.errorMapper(rawErr);
        const mappedKind = (mapped as { kind?: string } | null)?.kind;
        const isRetryable =
          mappedKind != null && config.retryPolicy.retryableErrorKinds.includes(mappedKind);
        const canRetry = isRetryable && attempt < config.retryPolicy.maxRetries;
        if (canRetry) {
          const delay = config.retryPolicy.backoffMs[attempt] ?? 0;
          flushSync(() => {
            setRetryCount(attempt + 1);
          });
          retryTimerRef.current = setTimeout(() => {
            void runStream(input, attempt + 1);
          }, delay);
        } else {
          flushSync(() => {
            setError(mapped);
          });
        }
      } finally {
        if (watchdogRef.current) {
          clearTimeout(watchdogRef.current);
          watchdogRef.current = null;
        }
      }
    },
    [config]
  );

  const ask = useCallback(
    (input: TInput) => {
      cleanup();
      setState(config.initialState);
      setRetryCount(0);
      setError(null);
      void runStream(input, 0);
    },
    [config.initialState, cleanup, runStream]
  );

  const stop = useCallback(() => {
    cleanup();
  }, [cleanup]);
  const reset = useCallback(() => {
    cleanup();
    setState(config.initialState);
    setRetryCount(0);
    setError(null);
  }, [config.initialState, cleanup]);

  return { state, retryCount, error, ask, stop, reset };
}
