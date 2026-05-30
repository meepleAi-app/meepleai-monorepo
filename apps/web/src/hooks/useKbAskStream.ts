/**
 * useKbAskStream — FSM 5-state SSE hook for /api/v1/knowledge-base/ask/global.
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
 * D-F: citations rendered as numbered list BELOW the answer (LLM does NOT emit
 * `[N]` inline markers; verified in `RagPromptAssemblyService.BuildSystemPrompt`).
 *
 * @see apps/web/src/hooks/useAgentChatStream.ts (parent pattern, different retry policy)
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { flushSync } from 'react-dom';

import { kbAskClient } from '../lib/api/clients/kbAskClient';

import type { KbAskRequest, KbCitation } from '../lib/api/schemas/kb-ask.schemas';

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

const INITIAL_STATE: KbAskStreamState = {
  status: 'idle',
  partialText: '',
  citations: [],
  totalTokens: 0,
  elapsedMs: 0,
  error: null,
  retryCount: 0,
};

const MAX_RETRIES = 3;
const RETRY_DELAYS_MS = [1000, 3000, 9000] as const;
const TIMEOUT_MS = 30_000;
const SERVER_ERROR_CODES = new Set([
  'RBAC_RESOLUTION_FAILED',
  'RETRIEVAL_FAILED',
  'PROMPT_ASSEMBLY_FAILED',
]);

export function useKbAskStream(): UseKbAskStream {
  const [state, setState] = useState<KbAskStreamState>(INITIAL_STATE);
  const abortRef = useRef<AbortController | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const startTimeRef = useRef<number>(0);

  // Cleanup on unmount
  useEffect(
    () => () => {
      abortRef.current?.abort();
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
    },
    []
  );

  const clearTimer = useCallback(() => {
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  }, []);

  const runStream = useCallback(
    async (body: KbAskRequest, retryIdx: number) => {
      const ac = new AbortController();
      abortRef.current = ac;
      startTimeRef.current = Date.now();

      // On retries (retryIdx > 0) preserve the current retryCount in state.
      // ask() resets it to 0 via INITIAL_STATE; each catch increments it.
      setState(s => ({ ...s, status: 'streaming', error: null, elapsedMs: 0 }));

      let hasCitations = false;
      let accumulated = '';

      // Timeout watchdog — started on the FIRST event (after connection is established)
      // to avoid the timer from being set during synchronous error paths (network throw
      // before any events). Reset on each subsequent event.
      let watchdogStarted = false;
      const resetTimeout = () => {
        clearTimer();
        timeoutRef.current = setTimeout(() => {
          ac.abort();
          setState(s => ({
            ...s,
            status: 'error',
            error: { kind: 'timeout', message: 'No response in 30s' },
          }));
        }, TIMEOUT_MS);
      };

      try {
        for await (const evt of kbAskClient.askGlobal(body, ac.signal)) {
          // Bail out if aborted (stop() was called during iteration)
          if (ac.signal.aborted) return;

          // Start/reset timeout watchdog on first received event.
          if (!watchdogStarted) {
            watchdogStarted = true;
          }
          resetTimeout();

          if (evt.type === 0) {
            // StateUpdate — no state change beyond noting we got an event
            continue;
          }
          if (evt.type === 1) {
            hasCitations = true;
            setState(s => ({ ...s, citations: evt.data.citations }));
            continue;
          }
          if (evt.type === 7) {
            accumulated += evt.data.token;
            const snap = accumulated;
            setState(s => ({
              ...s,
              partialText: snap,
              elapsedMs: Date.now() - startTimeRef.current,
            }));
            continue;
          }
          if (evt.type === 4) {
            clearTimer();
            const completedEmpty = evt.data.totalTokens === 0 && !hasCitations;
            setState(s => ({
              ...s,
              status: completedEmpty ? 'completed-empty' : 'completed',
              totalTokens: evt.data.totalTokens,
              elapsedMs: Date.now() - startTimeRef.current,
            }));
            return;
          }
          if (evt.type === 5) {
            clearTimer();
            const code = evt.data.code;
            const kind: KbAskErrorKind = SERVER_ERROR_CODES.has(code)
              ? 'server'
              : code === 'LLM_STREAMING_FAILED' && accumulated.length > 0
                ? 'partial'
                : 'server';
            setState(s => ({
              ...s,
              status: 'error',
              error: { kind, message: evt.data.message, code },
            }));
            return;
          }
        }
      } catch (err) {
        clearTimer();
        if (ac.signal.aborted) return; // stop() called — caller handles state
        const isNetwork = err instanceof TypeError;
        const errorMsg = err instanceof Error ? err.message : 'Unknown error';

        const nextRetry = retryIdx + 1;
        const errorKind: KbAskErrorKind = isNetwork ? 'connection' : 'server';

        // Set connection error immediately so consumers can observe it (test 9 needs this
        // to be visible within waitFor timeout with real timers).
        setState(s => ({
          ...s,
          status: 'error',
          error: { kind: errorKind, message: errorMsg },
          retryCount: isNetwork && retryIdx < MAX_RETRIES ? nextRetry : retryIdx,
        }));

        if (isNetwork && retryIdx < MAX_RETRIES) {
          // Retry with exp backoff. flushSync inside the timer callback forces React to
          // commit the retryCount update before runStream executes the next attempt.
          // This is needed for fake-timer tests (vi.runAllTimersAsync) where setState
          // from timer callbacks isn't automatically flushed before the assertion.
          const delay = RETRY_DELAYS_MS[retryIdx] ?? RETRY_DELAYS_MS[RETRY_DELAYS_MS.length - 1];
          timeoutRef.current = setTimeout(async () => {
            flushSync(() => {
              setState(s => ({
                ...s,
                status: 'error',
                error: { kind: 'connection', message: errorMsg },
                retryCount: nextRetry,
              }));
            });
            await runStream(body, nextRetry);
          }, delay);
        }
      }
    },
    [clearTimer]
  );

  const ask = useCallback(
    (query: string, opts?: AskOptions) => {
      abortRef.current?.abort();
      clearTimer();
      setState({ ...INITIAL_STATE, status: 'streaming' });
      const body: KbAskRequest = {
        query,
        ...(opts?.language && { language: opts.language }),
        ...(opts?.topK && { topK: opts.topK }),
      };
      void runStream(body, 0);
    },
    [runStream, clearTimer]
  );

  const stop = useCallback(() => {
    abortRef.current?.abort();
    clearTimer();
    setState(INITIAL_STATE);
  }, [clearTimer]);

  const reset = useCallback(() => {
    abortRef.current?.abort();
    clearTimer();
    setState(INITIAL_STATE);
  }, [clearTimer]);

  return { state, ask, stop, reset };
}
