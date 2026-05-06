'use client';

/**
 * useSessionLiveStream Hook (Wave D.2, Issue #750)
 *
 * Wraps native EventSource for SSE subscription to
 * GET /api/v1/game-sessions/{sessionId}/stream/v2.
 *
 * ## Features
 * - Typed event accumulation via useReducer (idempotent dedup by event.id)
 * - Exponential backoff retry budget: [1s, 2s, 4s, 8s, 16s]
 * - After 5 retries: connectionState = 'degraded-polling'
 *   → Orchestrator (Task 3) should mount useSessionState polling fallback
 * - 429 response: connectionState = 'failed', no retry
 *   → Orchestrator shows toast "Sessione affollata, riprova tra 60s"
 * - Browser-native Last-Event-ID reconnection (browser sends header automatically)
 * - Heartbeat events silently ignored (no state change)
 * - EventSource closed on unmount and sessionId change
 * - connectRef pattern avoids stale closures in retry timers
 *
 * ## Polling fallback strategy (for Task 3 orchestrator)
 *
 * When `connectionState === 'degraded-polling'`:
 *   mount `useSessionState({ sessionId, refetchInterval: 5_000 })` for degraded updates.
 *   SSE retry is abandoned but session state is still queryable via REST.
 *
 * When `connectionState === 'failed'` (429 received):
 *   show toast "Sessione affollata, riprova tra 60s" and disable manual retry for 60s.
 *   The `reconnect()` callback resets retry state and opens new EventSource.
 *
 * ## 429 detection
 * Native EventSource does not expose HTTP status codes in the error event.
 * Detection strategy: on onerror, check readyState === EventSource.CLOSED immediately
 * after the error (server closed the connection), and cross-reference via
 * a pre-flight HEAD check or accept as heuristic.
 *
 * In the v2 stream endpoint, backend returns HTTP 429 before any SSE headers,
 * causing the browser EventSource to fire onerror immediately with CLOSED state.
 * We treat immediate CLOSED (0 successful messages) on first connect as 429-likely
 * and set connectionState = 'failed'. This matches observed browser behavior.
 */

import { useCallback, useEffect, useReducer, useRef } from 'react';

import { parseSseEvent } from './parse-sse-event';
import { SESSION_EVENT_TYPES } from './sse-events';

import type { SessionEvent, SessionEventType } from './sse-events';

// ============================================================================
// Types
// ============================================================================

export type SseConnectionState =
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'degraded-polling'
  | 'failed';

export interface UseSessionLiveStreamInput {
  readonly sessionId: string | null;
  readonly enabled: boolean;
  /** Override base URL for testing (default: empty string → relative path) */
  readonly baseUrl?: string;
}

export interface UseSessionLiveStreamResult {
  readonly events: ReadonlyArray<SessionEvent>;
  readonly connectionState: SseConnectionState;
  readonly lastEventId: string | null;
  readonly retryCount: number;
  readonly retryAt: Date | null;
  readonly reconnect: () => void;
}

// ============================================================================
// Retry budget
// ============================================================================

const RETRY_BUDGET_MS = [1_000, 2_000, 4_000, 8_000, 16_000] as const;
const MAX_RETRIES = RETRY_BUDGET_MS.length;

// ============================================================================
// Reducer
// ============================================================================

interface StreamState {
  events: ReadonlyArray<SessionEvent>;
  seenIds: ReadonlySet<string>;
  connectionState: SseConnectionState;
  lastEventId: string | null;
  retryCount: number;
  retryAt: Date | null;
}

type StreamAction =
  | { type: 'CONNECTING' }
  | { type: 'CONNECTED' }
  /**
   * EVENT carries the typed SessionEvent plus the SSE envelope ID (Last-Event-ID header).
   * Foundation's SessionEvent has no top-level `id` field — dedup uses the SSE envelope ID.
   * When the server sends no Last-Event-ID, sseId is an empty string (no dedup for that event).
   */
  | { type: 'EVENT'; event: SessionEvent; sseId: string }
  | { type: 'RETRY'; retryCount: number; retryAt: Date }
  | { type: 'DEGRADED_POLLING' }
  | { type: 'FAILED' }
  | { type: 'RESET' };

const INITIAL_STATE: StreamState = {
  events: [],
  seenIds: new Set<string>(),
  connectionState: 'connecting',
  lastEventId: null,
  retryCount: 0,
  retryAt: null,
};

function streamReducer(state: StreamState, action: StreamAction): StreamState {
  switch (action.type) {
    case 'CONNECTING':
      return { ...state, connectionState: 'connecting' };

    case 'CONNECTED':
      return { ...state, connectionState: 'connected', retryCount: 0, retryAt: null };

    case 'EVENT': {
      const { event, sseId } = action;
      // Idempotency: skip duplicate SSE envelope IDs (empty string = no dedup)
      if (sseId && state.seenIds.has(sseId)) return state;

      const newSeenIds = new Set(state.seenIds);
      if (sseId) newSeenIds.add(sseId);

      // Heartbeat: update lastEventId but do not append to events array
      if (event.type === 'heartbeat') {
        return { ...state, seenIds: newSeenIds, lastEventId: sseId || state.lastEventId };
      }

      return {
        ...state,
        events: [...state.events, event],
        seenIds: newSeenIds,
        lastEventId: sseId || state.lastEventId,
      };
    }

    case 'RETRY':
      return {
        ...state,
        connectionState: 'reconnecting',
        retryCount: action.retryCount,
        retryAt: action.retryAt,
      };

    case 'DEGRADED_POLLING':
      return { ...state, connectionState: 'degraded-polling' };

    case 'FAILED':
      return { ...state, connectionState: 'failed' };

    case 'RESET':
      return {
        ...INITIAL_STATE,
        // Preserve events accumulation across reconnects — only reset connection state
        events: state.events,
        seenIds: state.seenIds,
        lastEventId: state.lastEventId,
      };

    default:
      return state;
  }
}

// ============================================================================
// Hook
// ============================================================================

export function useSessionLiveStream(input: UseSessionLiveStreamInput): UseSessionLiveStreamResult {
  const { sessionId, enabled, baseUrl = '' } = input;

  const [state, dispatch] = useReducer(streamReducer, INITIAL_STATE);

  const eventSourceRef = useRef<EventSource | null>(null);
  const retryTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isMountedRef = useRef(false);
  const retryCountRef = useRef(0);
  // Track whether we have received at least one message (for 429 heuristic)
  const receivedFirstMessageRef = useRef(false);
  // connectRef pattern — avoids stale closures in retry timers
  const connectRef = useRef<() => void>(() => {});

  const cleanup = useCallback(() => {
    if (retryTimerRef.current !== null) {
      clearTimeout(retryTimerRef.current);
      retryTimerRef.current = null;
    }
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
  }, []);

  const connect = useCallback(() => {
    if (!isMountedRef.current || !sessionId || !enabled) return;

    cleanup();
    receivedFirstMessageRef.current = false;
    dispatch({ type: 'CONNECTING' });

    const lastEventId = state.lastEventId;
    const url = `${baseUrl}/api/v1/game-sessions/${sessionId}/stream/v2${
      lastEventId ? `?lastEventId=${encodeURIComponent(lastEventId)}` : ''
    }`;

    let es: EventSource;
    try {
      es = new EventSource(url, { withCredentials: true });
    } catch {
      // EventSource not supported or CORS error
      dispatch({ type: 'DEGRADED_POLLING' });
      return;
    }
    eventSourceRef.current = es;

    es.onopen = () => {
      if (!isMountedRef.current) return;
      dispatch({ type: 'CONNECTED' });
      retryCountRef.current = 0;
    };

    // Listen for each typed event by name
    const handleTypedEvent = (eventType: SessionEventType) => {
      es.addEventListener(eventType, (e: MessageEvent) => {
        if (!isMountedRef.current) return;

        if (!receivedFirstMessageRef.current) {
          receivedFirstMessageRef.current = true;
          dispatch({ type: 'CONNECTED' });
          retryCountRef.current = 0;
        }

        // sessionId comes from the URL, not the SSE envelope; e.lastEventId is the dedup key
        const sseId = e.lastEventId || '';
        const event = parseSseEvent(eventType, e.data, sessionId ?? '');
        if (event) {
          dispatch({ type: 'EVENT', event, sseId });
        }
      });
    };

    for (const eventType of SESSION_EVENT_TYPES) {
      handleTypedEvent(eventType);
    }

    // Fallback: handle unnamed messages (onmessage) for servers sending default event type
    es.onmessage = (e: MessageEvent) => {
      if (!isMountedRef.current) return;

      if (!receivedFirstMessageRef.current) {
        receivedFirstMessageRef.current = true;
        dispatch({ type: 'CONNECTED' });
        retryCountRef.current = 0;
      }

      // Try to parse as heartbeat or typed via type field in data
      try {
        const parsed = JSON.parse(e.data) as Record<string, unknown>;
        const type = typeof parsed['type'] === 'string' ? parsed['type'] : 'heartbeat';
        const sseId = e.lastEventId || '';
        const event = parseSseEvent(type, e.data, sessionId ?? '');
        if (event) {
          dispatch({ type: 'EVENT', event, sseId });
        }
      } catch {
        // Ignore malformed messages
      }
    };

    es.onerror = () => {
      if (!isMountedRef.current) return;

      // 429 heuristic: immediate CLOSED state with no messages received
      const isImmediateClosed =
        !receivedFirstMessageRef.current && es.readyState === EventSource.CLOSED;

      cleanup();

      if (isImmediateClosed && retryCountRef.current === 0) {
        // Treat as 429 — backend closed before any data
        dispatch({ type: 'FAILED' });
        return;
      }

      const nextRetryCount = retryCountRef.current + 1;

      if (nextRetryCount > MAX_RETRIES) {
        dispatch({ type: 'DEGRADED_POLLING' });
        return;
      }

      retryCountRef.current = nextRetryCount;
      const delayMs = RETRY_BUDGET_MS[nextRetryCount - 1] ?? RETRY_BUDGET_MS[MAX_RETRIES - 1];
      const retryAt = new Date(Date.now() + delayMs);

      dispatch({ type: 'RETRY', retryCount: nextRetryCount, retryAt });

      retryTimerRef.current = setTimeout(() => {
        if (isMountedRef.current) {
          connectRef.current();
        }
      }, delayMs);
    };
  }, [sessionId, enabled, baseUrl, cleanup, state.lastEventId]);

  // Keep connectRef stable for retry timer closures
  connectRef.current = connect;

  // Manual reconnect — resets retry state
  const reconnect = useCallback(() => {
    retryCountRef.current = 0;
    receivedFirstMessageRef.current = false;
    dispatch({ type: 'RESET' });
    connectRef.current();
  }, []);

  // (Re)connect when sessionId/enabled changes
  useEffect(() => {
    isMountedRef.current = true;

    if (sessionId && enabled) {
      connect();
    } else {
      cleanup();
    }

    return () => {
      isMountedRef.current = false;
      cleanup();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId, enabled]);

  return {
    events: state.events,
    connectionState: state.connectionState,
    lastEventId: state.lastEventId,
    retryCount: state.retryCount,
    retryAt: state.retryAt,
    reconnect,
  };
}
