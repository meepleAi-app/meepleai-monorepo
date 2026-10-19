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
  /**
   * Maps raw thrown errors (or special "error" events) to consumer's TError shape.
   * Two sentinels come from the FSM itself, as `{ __sseStreamFsmReason }`: `'timeout'` (watchdog)
   * and `'interrupted'` (the stream ended without a terminal event, see `isTerminalEvent`).
   */
  errorMapper: (err: unknown) => TError;
  /**
   * #4335: the event that concludes a response (e.g. `Complete` or `Error`). When given, a stream
   * that ends cleanly WITHOUT one of these — the backend or a proxy closing the connection halfway —
   * is an interruption: it goes through `errorMapper` as `{ __sseStreamFsmReason: 'interrupted' }`,
   * and from there to retry or to `error` like any thrown error. An error thrown AFTER a terminal
   * event is ignored: the response was already closed. When omitted, the end of the stream means
   * nothing, as before.
   */
  isTerminalEvent?: (event: TEvent) => boolean;
  retryPolicy: {
    maxRetries: number;
    /** Backoff schedule. Must have length >= maxRetries. Example: [1000, 3000, 9000] for maxRetries=3. */
    backoffMs: readonly number[];
    /** When defined, lazy watchdog timer fires if no event arrives within timeoutMs (resets per event). */
    timeoutMs?: number;
    /**
     * String-kind whitelist. The errorMapper output is checked by reading the error's `kind`
     * property — if it matches one of these, the FSM schedules a retry.
     *
     * A retry starts from the ACCUMULATED state, not from `initialState`: the events of the new
     * attempt are reduced on top of those of the failed one. So map an error to a retryable kind
     * only when the failed attempt reduced nothing that the new one would repeat — in particular
     * `'interrupted'` and any error thrown mid-stream, once tokens have arrived (they would be
     * appended twice). `useAgentChatStream` does it with its `currentAnswerRef` guard.
     *
     * Not reset on purpose: between attempts the state still shows the reduced "connection lost,
     * retrying" event (`useAgentChatStream`), and a reset would drop it and flip the view to "not
     * streaming" in the middle of the backoff, re-enabling inputs while a retry is pending.
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

      const fail = (rawErr: unknown) => {
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
      };

      let sawTerminalEvent = false;
      let interrupted = false;
      try {
        for await (const event of config.transport(input, controller.signal)) {
          if (controller.signal.aborted) return;
          if (!watchdogStarted) {
            watchdogStarted = true;
            armWatchdog();
          } else armWatchdog();
          if (config.isTerminalEvent?.(event)) sawTerminalEvent = true;
          flushSync(() => {
            setState(s => config.eventReducer(s, event));
          });
        }
        // #4335: the transport returned without throwing. A stop by the user ends here too (the
        // transport returns on the signal), and that is not an interruption.
        interrupted =
          !controller.signal.aborted && config.isTerminalEvent != null && !sawTerminalEvent;
      } catch (rawErr) {
        // Review of #4509: after the terminal event the response is closed. What the stream does
        // after it (the backend keeps the connection open for the follow-up questions) can fail,
        // but it is not a failure of the response.
        if (controller.signal.aborted || sawTerminalEvent) return;
        fail(rawErr);
      } finally {
        if (watchdogRef.current) {
          clearTimeout(watchdogRef.current);
          watchdogRef.current = null;
        }
      }
      // The interruption takes the same road as any other failure: errorMapper, then retry or
      // error, as the consumer decides.
      if (interrupted) fail({ __sseStreamFsmReason: 'interrupted' });
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
