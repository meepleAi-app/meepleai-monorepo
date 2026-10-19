/**
 * useKbAskStream — thin wrapper over useSseStreamFsm for /api/v1/knowledge-base/ask/global.
 *
 * States (D-L spec-panel 2026-05-30):
 *   idle → streaming → completed | completed-empty | error
 *
 * Error sub-kinds:
 *   - connection: fetch throw (TypeError) before any word of the answer
 *   - timeout: 30s without any event (TIMEOUT_MS), before any word of the answer
 *   - interrupted (#4510): the answer stopped before its end — the stream closed without Complete
 *     or Error; after some words, an error thrown, the 30s watchdog, or any Error event but the plan
 *     quota; or the backend said `AI_STREAM_INTERRUPTED`. partialText preserved (it may be empty);
 *     the message is the main chat's one, the backend's code (if any) is kept.
 *     It replaces `partial`, tied to `LLM_STREAMING_FAILED`, a code the backend no longer sends.
 *   - server: an Error event before any word (RBAC_RESOLUTION_FAILED / RETRIEVAL_FAILED /
 *     PROMPT_ASSEMBLY_FAILED / ...), the plan quota (TIER_LIMIT_EXCEEDED), or another thrown error
 *   - completed-empty: Complete arrives with totalTokens=0 and NO prior Citations
 *     (Adzic edge case — user has no accessible games)
 *
 * Retry policy: exp backoff [1s, 3s, 9s], max 3 retries, only on `connection` kind — so never once
 * a word of the answer has arrived (#4510: the retry starts from the accumulated state and would
 * append the answer again under it).
 *
 * @see apps/web/src/hooks/useSseStreamFsm.ts (base FSM hook)
 * @see apps/web/src/hooks/useAgentChatStream.ts (parent pattern, different retry policy)
 */

import { useCallback, useMemo, useRef } from 'react';

import { qaStreamErrorMessage } from '@/components/chat-unified/qaStreamErrors';
import { TIER_LIMIT_EXCEEDED } from '@/lib/tier-limit';

import { useSseStreamFsm, type SseStreamFsmConfig } from './useSseStreamFsm';
import { kbAskClient } from '../lib/api/clients/kbAskClient';

import type {
  KbAskEvent,
  KbAskRequest,
  KbCitation,
  KbTierLimit,
} from '../lib/api/schemas/kb-ask.schemas';

export type KbAskStatus = 'idle' | 'streaming' | 'completed' | 'completed-empty' | 'error';
export type KbAskErrorKind = 'connection' | 'timeout' | 'interrupted' | 'server';

export interface KbAskError {
  readonly kind: KbAskErrorKind;
  readonly message: string;
  readonly code?: string;
  /**
   * #4168 (5g-b): solo quando il codice è `TIER_LIMIT_EXCEEDED`. I numeri del piano e il link ai
   * piani, come li manda il backend: chi li mostra li valida con `readTierLimit`.
   */
  readonly tierLimit?: KbTierLimit;
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
  /** #4510: asks the last question again, from scratch. Nothing to do before the first `ask`. */
  retry: () => void;
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
  /**
   * #4510: the attempt whose events this state last reduced — 1 for the first, one more for each
   * retry, 0 before any event. Internal: it tells whether the retry after a failed attempt has
   * answered yet (see `mergedState`).
   */
  readonly attempt: number;
}

const INITIAL_INTERNAL_STATE: KbInternalState = {
  status: 'idle',
  partialText: '',
  citations: [],
  totalTokens: 0,
  elapsedMs: 0,
  error: null,
  attempt: 0,
};

/** #4510: the backend's own code for an answer cut off after its first word (LLM gateway). */
const AI_STREAM_INTERRUPTED = 'AI_STREAM_INTERRUPTED';

/**
 * #4510: the answer stopped before its end. The sentence the main chat shows (`qaStreamErrors`,
 * also used by `useAgentChatStream` since #4335): for the user it is the same thing.
 */
const STREAM_INTERRUPTED_MESSAGE = qaStreamErrorMessage(AI_STREAM_INTERRUPTED);

/** #4510: a response ends with Complete or Error. A stream that closes before either was cut off. */
function isTerminalKbEvent(event: KbAskEvent): boolean {
  return event.type === 4 || event.type === 5;
}

function isFsmSentinel(raw: unknown, reason: 'timeout' | 'interrupted'): boolean {
  return (raw as { __sseStreamFsmReason?: string } | null)?.__sseStreamFsmReason === reason;
}

export function useKbAskStream(): UseKbAskStream {
  // Track per-stream start time so the reducer can compute elapsedMs on Token + Complete.
  const startTimeRef = useRef<number>(0);
  // Stash the last connection error so it's visible even while a retry is pending.
  // The base FSM only calls setError on final failure; we need the error on the FIRST failure
  // (test: "Network throw maps to kind=connection" does waitFor without advancing timers).
  const pendingErrorRef = useRef<KbAskError | null>(null);
  // #4510: which attempt of the current question is running (1 for the first). Stamped on the
  // state by the reducer, so the merged state can tell when the retry has answered.
  const attemptRef = useRef(0);
  // #4510: the words of the answer on screen. errorMapper reads it: once some have arrived, a
  // failure is an interruption, never a retry (it would append the answer again under them).
  const partialTextRef = useRef('');
  // #4510: the last question, for `retry()`.
  const lastRequestRef = useRef<KbAskRequest | null>(null);

  const config = useMemo<SseStreamFsmConfig<KbAskRequest, KbAskEvent, KbInternalState, KbAskError>>(
    () => ({
      transport: (input, signal) => {
        startTimeRef.current = Date.now();
        attemptRef.current += 1;
        return kbAskClient.askGlobal(input, signal);
      },
      initialState: INITIAL_INTERNAL_STATE,
      eventReducer: (state, event) => {
        const next = reduceKbEvent(state, event, startTimeRef.current);
        // attemptRef is only read here, and partialTextRef gets the same value on each call: the
        // reducer stays safe under StrictMode's double call.
        const stamped =
          next.attempt === attemptRef.current ? next : { ...next, attempt: attemptRef.current };
        partialTextRef.current = stamped.partialText;
        return stamped;
      },
      isTerminalEvent: isTerminalKbEvent,
      // errorMapper handles thrown exceptions (network errors, timeout signal) and, since #4510,
      // the interruption: the stream ended without Complete or Error.
      // IMPORTANT: also stash to pendingErrorRef so it's visible during retries.
      errorMapper: raw => {
        let mapped: KbAskError;
        if (isFsmSentinel(raw, 'interrupted') || partialTextRef.current !== '') {
          // #4510: never retried. With words already on screen, the retry would start from the
          // accumulated state and append the answer again under them ("La La "). Without, the
          // connection did work and the server (or a proxy) closed it: not the failure to connect
          // the retry is for, and a silent resend is one more model call nobody asked for. Same
          // rule as the chat (#4335). The user asks again, with the reason in view.
          // Review of #4524: the watchdog too, once words have arrived — for the user the answer
          // stopped before its end, and the words stay (a `timeout` would hide them).
          mapped = { kind: 'interrupted', message: STREAM_INTERRUPTED_MESSAGE };
        } else if (isFsmSentinel(raw, 'timeout')) {
          // Timeout sentinel injected by useSseStreamFsm watchdog, before any word of the answer
          mapped = { kind: 'timeout', message: 'No response in 30s' };
        } else if (raw instanceof TypeError) {
          // Network / connection error, before any word of the answer
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
    // #4510: the error of a failed attempt stands while its retry is pending, and until the retry
    // answers — its first event is reduced with `attempt` past the failed ones. Before, it stood
    // for good: a retry that completed the answer still said `status: 'error'`.
    const retryPending = retryCount > 0 && state.attempt <= retryCount;
    // Priority: SSE Error event (reducer) > thrown exception (base hook final) > pending retry error
    const mergedError: KbAskError | null =
      state.error ?? thrownError ?? (retryPending ? pendingErrorRef.current : null);
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

  /** A new question (or the same one again) starts from nothing. */
  const clearRun = useCallback(() => {
    pendingErrorRef.current = null;
    attemptRef.current = 0;
    partialTextRef.current = '';
  }, []);

  // Wrap ask to accept (query: string, opts?: AskOptions) and build KbAskRequest.
  const ask = useCallback(
    (query: string, opts?: AskOptions) => {
      clearRun();
      const body: KbAskRequest = {
        query,
        ...(opts?.language !== undefined && { language: opts.language }),
        ...(opts?.topK !== undefined && { topK: opts.topK }),
      };
      lastRequestRef.current = body;
      baseAsk(body);
    },
    [baseAsk, clearRun]
  );

  // #4510: "Riprova a chiedere" — the same question, from scratch (baseAsk resets the state).
  const retry = useCallback(() => {
    const last = lastRequestRef.current;
    if (last === null) return;
    clearRun();
    baseAsk(last);
  }, [baseAsk, clearRun]);

  // stop() should reset to idle (base hook stop() only aborts, does not reset state).
  // Using baseReset so state goes back to INITIAL_INTERNAL_STATE (idle).
  const stop = useCallback(() => {
    clearRun();
    baseReset();
  }, [baseReset, clearRun]);

  const reset = useCallback(() => {
    clearRun();
    baseReset();
  }, [baseReset, clearRun]);

  return { state: mergedState, ask, retry, stop, reset };
}

/** Applies one event of `/ask/global` to the state. Pure, except for the clock. */
function reduceKbEvent(
  state: KbInternalState,
  event: KbAskEvent,
  startTime: number
): KbInternalState {
  // #4510 (review of #4509): after Complete the answer is whole. An Error that comes after it does
  // not make it fail — nor does a transport failure, which the FSM already ignores.
  if (state.status === 'completed' || state.status === 'completed-empty') return state;

  // Transition to streaming on first event (if still idle)
  const next: KbInternalState = state.status === 'idle' ? { ...state, status: 'streaming' } : state;

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
        elapsedMs: Date.now() - startTime,
      };

    case 5: {
      // SSE Error event — handled in reducer (not thrown, so won't go through errorMapper)
      const code = event.data.code;
      // #4510: the backend says the answer stopped after its first word — or (review of #4524)
      // any error comes after some words, e.g. `INTERNAL_ERROR` from the endpoint's `catch`. The
      // same interruption as a stream that closes halfway, with the same sentence: the words stay,
      // as incomplete, and the backend's code with them. Not the plan quota: it has its own notice.
      if (
        code === AI_STREAM_INTERRUPTED ||
        (next.partialText !== '' && code !== TIER_LIMIT_EXCEEDED)
      ) {
        return {
          ...next,
          status: 'error',
          error: { kind: 'interrupted', message: STREAM_INTERRUPTED_MESSAGE, code },
        };
      }
      return {
        ...next,
        status: 'error',
        error: {
          kind: 'server',
          message: event.data.message,
          code,
          ...(event.data.tierLimit ? { tierLimit: event.data.tierLimit } : {}),
        },
      };
    }

    case 7: // Token
      return {
        ...next,
        partialText: next.partialText + event.data.token,
        elapsedMs: Date.now() - startTime,
      };

    default:
      return next;
  }
}
