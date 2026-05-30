/**
 * useKbAskStream — thin wrapper over useSseStreamFsm for /api/v1/knowledge-base/ask/global.
 *
 * States (D-L spec-panel 2026-05-30):
 *   idle → streaming → completed | completed-empty | error
 *
 * Error sub-kinds:
 *   - connection: fetch throw (TypeError) / network drop
 *   - timeout: 30s without any event (TIMEOUT_MS)
 *   - partial: LLM_STREAMING_FAILED mid-stream (partialText preserved)
 *   - server: RBAC_RESOLUTION_FAILED / RETRIEVAL_FAILED / PROMPT_ASSEMBLY_FAILED
 *   - completed-empty: Complete arrives with totalTokens=0 and NO prior Citations
 *     (Adzic edge case — user has no accessible games)
 *
 * Retry policy: exp backoff [1s, 3s, 9s], max 3 retries, only on `connection` kind.
 *
 * @see apps/web/src/hooks/useSseStreamFsm.ts (base FSM hook)
 * @see apps/web/src/hooks/useAgentChatStream.ts (parent pattern, different retry policy)
 */

import { useCallback, useMemo, useRef } from 'react';

import { useSseStreamFsm, type SseStreamFsmConfig } from './useSseStreamFsm';
import { kbAskClient } from '../lib/api/clients/kbAskClient';

import type { KbAskEvent, KbAskRequest, KbCitation } from '../lib/api/schemas/kb-ask.schemas';

export type KbAskStatus = 'idle' | 'streaming' | 'completed' | 'completed-empty' | 'error';
export type KbAskErrorKind = 'connection' | 'timeout' | 'partial' | 'server';

export interface KbAskError {
  readonly kind: KbAskErrorKind;
  readonly message: string;
  readonly code?: string;
}

export interface KbAskStreamState {
  readonly status: KbAskStatus;
  readonly partialText: string;
  readonly citations: readonly KbCitation[];
  readonly totalTokens: number;
  readonly elapsedMs: number;
  readonly error: KbAskError | null;
  readonly retryCount: number;
}

export interface AskOptions {
  language?: string;
  topK?: number;
}

export interface UseKbAskStream {
  readonly state: KbAskStreamState;
  ask: (query: string, opts?: AskOptions) => void;
  stop: () => void;
  reset: () => void;
}

/**
 * Internal state tracked by the reducer inside useSseStreamFsm.
 * Includes `error` so that SSE Error events (type 5) can set error state
 * without going through the catch path (which is for thrown exceptions only).
 */
interface KbInternalState {
  readonly status: KbAskStatus;
  readonly partialText: string;
  readonly citations: readonly KbCitation[];
  readonly totalTokens: number;
  readonly elapsedMs: number;
  readonly error: KbAskError | null;
}

const INITIAL_INTERNAL_STATE: KbInternalState = {
  status: 'idle',
  partialText: '',
  citations: [],
  totalTokens: 0,
  elapsedMs: 0,
  error: null,
};

const SERVER_ERROR_CODES = new Set([
  'RBAC_RESOLUTION_FAILED',
  'RETRIEVAL_FAILED',
  'PROMPT_ASSEMBLY_FAILED',
]);

export function useKbAskStream(): UseKbAskStream {
  // Track per-stream start time so the reducer can compute elapsedMs on Token + Complete.
  const startTimeRef = useRef<number>(0);
  // Stash the last connection error so it's visible even while a retry is pending.
  // The base FSM only calls setError on final failure; we need the error on the FIRST failure
  // (test: "Network throw maps to kind=connection" does waitFor without advancing timers).
  const pendingErrorRef = useRef<KbAskError | null>(null);

  const config = useMemo<SseStreamFsmConfig<KbAskRequest, KbAskEvent, KbInternalState, KbAskError>>(
    () => ({
      transport: (input, signal) => {
        startTimeRef.current = Date.now();
        return kbAskClient.askGlobal(input, signal);
      },
      initialState: INITIAL_INTERNAL_STATE,
      eventReducer: (state, event) => {
        // Transition to streaming on first event (if still idle)
        const next: KbInternalState =
          state.status === 'idle' ? { ...state, status: 'streaming' } : state;

        switch (event.type) {
          case 0: // StateUpdate — no state change beyond ensuring streaming
            return next;

          case 1: // Citations
            return { ...next, citations: event.data.citations };

          case 4: // Complete
            return {
              ...next,
              status:
                event.data.totalTokens === 0 && next.citations.length === 0
                  ? 'completed-empty'
                  : 'completed',
              totalTokens: event.data.totalTokens,
              elapsedMs: Date.now() - startTimeRef.current,
            };

          case 5: {
            // SSE Error event — handled in reducer (not thrown, so won't go through errorMapper)
            const code = event.data.code;
            const kind: KbAskErrorKind = SERVER_ERROR_CODES.has(code)
              ? 'server'
              : code === 'LLM_STREAMING_FAILED' && next.partialText.length > 0
                ? 'partial'
                : 'server';
            return {
              ...next,
              status: 'error',
              error: { kind, message: event.data.message, code },
            };
          }

          case 7: // Token
            return {
              ...next,
              partialText: next.partialText + event.data.token,
              elapsedMs: Date.now() - startTimeRef.current,
            };

          default:
            return next;
        }
      },
      // errorMapper handles thrown exceptions (network errors, timeout signal).
      // IMPORTANT: also stash to pendingErrorRef so it's visible during retries.
      errorMapper: raw => {
        let mapped: KbAskError;
        // Timeout sentinel injected by useSseStreamFsm watchdog
        if ((raw as { __sseStreamFsmReason?: string } | null)?.__sseStreamFsmReason === 'timeout') {
          mapped = { kind: 'timeout', message: 'No response in 30s' };
        } else if (raw instanceof TypeError) {
          // Network / connection error
          mapped = { kind: 'connection', message: raw.message };
        } else {
          // Generic error
          mapped = { kind: 'server', message: raw instanceof Error ? raw.message : String(raw) };
        }
        // Always stash so it's visible while a retry is pending (base FSM only sets error
        // on final failure, but consumers expect to see the error immediately on each attempt).
        pendingErrorRef.current = mapped;
        return mapped;
      },
      retryPolicy: {
        maxRetries: 3,
        backoffMs: [1000, 3000, 9000],
        timeoutMs: 30_000,
        retryableErrorKinds: ['connection'],
      },
    }),
    []
  );

  const {
    state,
    retryCount,
    error: thrownError,
    ask: baseAsk,
    reset: baseReset,
  } = useSseStreamFsm(config);

  // Derive merged state. Deps include retryCount so the memo recomputes when a retry fires
  // (retryCount changes → memo re-runs → reads pendingErrorRef.current which was already stashed
  //  by errorMapper before setRetryCount was called).
  const mergedState = useMemo<KbAskStreamState>(() => {
    // Priority: SSE Error event (reducer) > thrown exception (base hook final) > pending retry error
    const mergedError: KbAskError | null =
      state.error ?? thrownError ?? (retryCount > 0 ? pendingErrorRef.current : null);
    const mergedStatus: KbAskStatus = mergedError != null ? 'error' : state.status;
    return {
      status: mergedStatus,
      partialText: state.partialText,
      citations: state.citations,
      totalTokens: state.totalTokens,
      elapsedMs: state.elapsedMs,
      error: mergedError,
      retryCount,
    };
  }, [state, thrownError, retryCount]);

  // Wrap ask to accept (query: string, opts?: AskOptions) and build KbAskRequest.
  const ask = useCallback(
    (query: string, opts?: AskOptions) => {
      pendingErrorRef.current = null;
      const body: KbAskRequest = {
        query,
        ...(opts?.language !== undefined && { language: opts.language }),
        ...(opts?.topK !== undefined && { topK: opts.topK }),
      };
      baseAsk(body);
    },
    [baseAsk]
  );

  // stop() should reset to idle (base hook stop() only aborts, does not reset state).
  // Using baseReset so state goes back to INITIAL_INTERNAL_STATE (idle).
  const stop = useCallback(() => {
    pendingErrorRef.current = null;
    baseReset();
  }, [baseReset]);

  const reset = useCallback(() => {
    pendingErrorRef.current = null;
    baseReset();
  }, [baseReset]);

  return { state: mergedState, ask, stop, reset };
}
