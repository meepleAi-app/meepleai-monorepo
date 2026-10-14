/**
 * useSessionSync Hook (Issue #3163)
 *
 * Session-scoped SSE streaming for generic toolkit real-time score sync.
 * Listens to ScoreUpdatedEvent from backend SSE infrastructure (GST-003).
 *
 * Features:
 * - Real-time score updates via SSE
 * - Connection status tracking
 * - Auto-reconnect with exponential backoff
 * - Error handling and recovery
 *
 * Backend endpoint (GST-003):
 * - GET /api/v1/game-sessions/{sessionId}/stream
 *
 * @example
 * ```typescript
 * const { scores, isConnected, error } = useSessionSync({
 *   sessionId: 'abc123',
 *   onScoreUpdate: (scoreEntry) => console.log('New score:', scoreEntry),
 * });
 * ```
 */

import { useState, useCallback, useRef, useEffect } from 'react';

import type { ScoreEntry, TurnAdvancedPayload } from '@/components/session/types';
import { logger } from '@/lib/logger';

/**
 * The events the toolkit pages use, named as the live session stream names them (#4114).
 *
 * The hook used to open `/game-sessions/{id}/stream` with a live session id (a 404), listen to
 * `ScoreUpdatedEvent`, `SessionPausedEvent`, … — the CLR names of the SessionTracking stream — and
 * read a `type` field that none of those events carries: score, pause, resume and finalize never
 * fired, on either stream. The live stream names its events `session:*` and sends the bare payload.
 */
const SESSION_EVENTS = {
  score: 'session:score',
  paused: 'session:pause',
  resumed: 'session:resume',
  finalized: 'session:endgame',
  toolkit: 'session:toolkit',
} as const;

/** `session:score` of the live stream: one cell of the session's RoundScores. */
interface LiveScorePayload {
  playerId: string;
  round: number;
  dimension: string;
  value: number;
}

function isLiveScorePayload(value: unknown): value is LiveScorePayload {
  if (typeof value !== 'object' || value === null) return false;
  const data = value as Record<string, unknown>;
  return (
    typeof data.playerId === 'string' &&
    typeof data.round === 'number' &&
    typeof data.dimension === 'string' &&
    typeof data.value === 'number'
  );
}

/** `session:toolkit` carries dice, cards, notes and widgets too: only a turn advance has these. */
function isTurnAdvancedPayload(value: unknown): value is TurnAdvancedPayload {
  if (typeof value !== 'object' || value === null) return false;
  const data = value as Record<string, unknown>;
  return typeof data.currentPlayerName === 'string' && typeof data.roundNumber === 'number';
}

/**
 * Hook options
 */
export interface UseSessionSyncOptions {
  /** Session ID */
  sessionId: string;

  /** Callback when a new score is added */
  onScoreUpdate?: (scoreEntry: ScoreEntry) => void;

  /** Callback when session is paused */
  onPaused?: () => void;

  /** Callback when session is resumed */
  onResumed?: () => void;

  /** Callback when session is finalized */
  onFinalized?: () => void;

  /** Callback when an error occurs */
  onError?: (error: Error) => void;

  /**
   * Callback for real-time turn-advanced events (`session:toolkit` SSE event).
   * Issue #4975 — TurnOrder Component.
   */
  onTurnAdvanced?: (payload: TurnAdvancedPayload) => void;

  /** API base URL */
  apiBaseUrl?: string;
}

/**
 * Hook return value
 */
export interface SessionSyncState {
  /** Whether SSE connection is active */
  isConnected: boolean;

  /** Error if connection failed */
  error: Error | null;

  /** Recent score updates */
  scores: ScoreEntry[];
}

/**
 * useSessionSync Hook
 *
 * Manages SSE connection for real-time session updates
 */
export function useSessionSync(options: UseSessionSyncOptions): SessionSyncState {
  const {
    sessionId,
    onScoreUpdate,
    onPaused,
    onResumed,
    onFinalized,
    onError,
    onTurnAdvanced,
    apiBaseUrl,
  } = options;

  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [scores, setScores] = useState<ScoreEntry[]>([]);

  // EventSource instance
  const eventSourceRef = useRef<EventSource | null>(null);

  // Reconnection state
  const reconnectAttemptsRef = useRef(0);
  const maxReconnectAttempts = 5;
  const reconnectTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  // Keep the latest callbacks in refs so the SSE connection is NOT torn down and
  // re-established when a consumer passes fresh inline callbacks on every render
  // (e.g. toolkit/[sessionId]/_content.tsx). Without this, `handleEvent` → `connect`
  // → the mount effect all get new identities each render, closing and re-opening
  // the EventSource in a self-sustaining loop that drops in-flight events and
  // flickers `isConnected`. Mirrors the ref pattern in useTurnOrder.
  const onScoreUpdateRef = useRef(onScoreUpdate);
  const onPausedRef = useRef(onPaused);
  const onResumedRef = useRef(onResumed);
  const onFinalizedRef = useRef(onFinalized);
  const onErrorRef = useRef(onError);
  const onTurnAdvancedRef = useRef(onTurnAdvanced);

  useEffect(() => {
    onScoreUpdateRef.current = onScoreUpdate;
    onPausedRef.current = onPaused;
    onResumedRef.current = onResumed;
    onFinalizedRef.current = onFinalized;
    onErrorRef.current = onError;
    onTurnAdvancedRef.current = onTurnAdvanced;
  });

  /**
   * Handle an SSE event: the SSE event name is the type, the data is the bare payload.
   */
  const handleEvent = useCallback((event: MessageEvent<string>) => {
    try {
      const data: unknown = JSON.parse(event.data);

      switch (event.type) {
        case SESSION_EVENTS.score: {
          if (!isLiveScorePayload(data)) {
            logger.error(`[useSessionSync] Unexpected session:score payload: ${event.data}`);
            break;
          }
          const scoreEntry: ScoreEntry = {
            id: `${data.playerId}-${data.round}-${data.dimension}`,
            participantId: data.playerId,
            roundNumber: data.round,
            category: data.dimension,
            scoreValue: data.value,
            timestamp: new Date(),
            createdBy: data.playerId,
          };
          setScores(prev => [...prev, scoreEntry]);
          onScoreUpdateRef.current?.(scoreEntry);
          break;
        }

        case SESSION_EVENTS.paused:
          onPausedRef.current?.();
          break;

        case SESSION_EVENTS.resumed:
          onResumedRef.current?.();
          break;

        case SESSION_EVENTS.finalized:
          onFinalizedRef.current?.();
          break;

        case SESSION_EVENTS.toolkit:
          // Turn-order events (Issue #4975); the other toolkit events are not this hook's.
          if (isTurnAdvancedPayload(data)) onTurnAdvancedRef.current?.(data);
          break;
      }
    } catch (err) {
      logger.error(
        `[useSessionSync] Event parsing error: ${event.data}`,
        err instanceof Error ? err : undefined
      );
    }
  }, []);

  /**
   * Connect to SSE stream
   */
  const connect = useCallback(() => {
    const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';
    // #4114: the toolkit pages hold the id of a LiveGameSession. Its stream fans in the companion
    // SessionTracking channel too, so it carries `session:toolkit` (dice, cards, notes, timers)
    // as well as the live session's own events.
    const endpoint = `${baseUrl}/api/v1/live-sessions/${sessionId}/stream`;

    try {
      const eventSource = new EventSource(endpoint, {
        withCredentials: true,
      });

      eventSource.onopen = () => {
        setIsConnected(true);
        setError(null);
        reconnectAttemptsRef.current = 0;
        logger.warn('[useSessionSync] Connected to SSE stream');
      };

      eventSource.onerror = err => {
        logger.error('[useSessionSync] SSE error:', err);
        setIsConnected(false);

        const errorObj = new Error('SSE connection failed');
        setError(errorObj);
        onErrorRef.current?.(errorObj);

        // Auto-reconnect with exponential backoff
        if (reconnectAttemptsRef.current < maxReconnectAttempts) {
          const delay = Math.min(Math.pow(2, reconnectAttemptsRef.current) * 1000, 30000);
          reconnectAttemptsRef.current += 1;

          logger.warn(
            `[useSessionSync] Reconnecting in ${delay / 1000}s (attempt ${reconnectAttemptsRef.current}/${maxReconnectAttempts})`
          );

          reconnectTimeoutRef.current = setTimeout(() => {
            eventSource.close();
            connect();
          }, delay);
        } else {
          logger.error('[useSessionSync] Max reconnection attempts reached');
          eventSource.close();
        }
      };

      // The callbacks are invoked through refs, so `connect` stays stable across renders.
      Object.values(SESSION_EVENTS).forEach(eventType => {
        eventSource.addEventListener(eventType, handleEvent);
      });

      eventSourceRef.current = eventSource;
    } catch (err) {
      const errorObj = err instanceof Error ? err : new Error('Failed to create EventSource');
      setError(errorObj);
      setIsConnected(false);
      onErrorRef.current?.(errorObj);
    }
  }, [sessionId, apiBaseUrl, handleEvent]);

  /**
   * Disconnect SSE stream
   */
  const disconnect = useCallback(() => {
    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }

    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }

    setIsConnected(false);
    reconnectAttemptsRef.current = 0;
  }, []);

  /**
   * Connect on mount, disconnect on unmount
   */
  useEffect(() => {
    connect();
    return () => disconnect();
  }, [connect, disconnect]);

  return {
    isConnected,
    error,
    scores,
  };
}
