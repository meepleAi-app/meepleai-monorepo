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

/**
 * SSE Event Types (from GST-003 backend)
 */
enum SessionEventType {
  ScoreUpdatedEvent = 'ScoreUpdatedEvent',
  SessionPausedEvent = 'SessionPausedEvent',
  SessionResumedEvent = 'SessionResumedEvent',
  SessionFinalizedEvent = 'SessionFinalizedEvent',
}

/**
 * SSE Event Data
 */
interface SessionEvent {
  type: SessionEventType;
  data: unknown;
}

interface ScoreUpdatedEventData {
  scoreEntry: ScoreEntry;
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

  /**
   * Handle SSE event
   */
  const handleEvent = useCallback(
    (event: MessageEvent<string>) => {
      try {
        const parsedEvent: SessionEvent = JSON.parse(event.data);

        switch (parsedEvent.type) {
          case SessionEventType.ScoreUpdatedEvent: {
            const data = parsedEvent.data as ScoreUpdatedEventData;
            const scoreEntry = {
              ...data.scoreEntry,
              timestamp: new Date(data.scoreEntry.timestamp),
            };

            setScores(prev => [...prev, scoreEntry]);
            onScoreUpdate?.(scoreEntry);
            break;
          }

          case SessionEventType.SessionPausedEvent: {
            onPaused?.();
            break;
          }

          case SessionEventType.SessionResumedEvent: {
            onResumed?.();
            break;
          }

          case SessionEventType.SessionFinalizedEvent: {
            onFinalized?.();
            break;
          }
        }
      } catch (err) {
        console.error('[useSessionSync] Event parsing error:', err, event.data);
      }
    },
    [onScoreUpdate, onPaused, onResumed, onFinalized]
  );

  /**
   * Connect to SSE stream
   */
  const connect = useCallback(() => {
    const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';
    const endpoint = `${baseUrl}/api/v1/game-sessions/${sessionId}/stream`;

    try {
      const eventSource = new EventSource(endpoint, {
        withCredentials: true,
      });

      eventSource.onopen = () => {
        setIsConnected(true);
        setError(null);
        reconnectAttemptsRef.current = 0;
        console.warn('[useSessionSync] Connected to SSE stream');
      };

      eventSource.onerror = err => {
        console.error('[useSessionSync] SSE error:', err);
        setIsConnected(false);

        const errorObj = new Error('SSE connection failed');
        setError(errorObj);
        onError?.(errorObj);

        // Auto-reconnect with exponential backoff
        if (reconnectAttemptsRef.current < maxReconnectAttempts) {
          const delay = Math.min(Math.pow(2, reconnectAttemptsRef.current) * 1000, 30000);
          reconnectAttemptsRef.current += 1;

          console.warn(
            `[useSessionSync] Reconnecting in ${delay / 1000}s (attempt ${reconnectAttemptsRef.current}/${maxReconnectAttempts})`
          );

          reconnectTimeoutRef.current = setTimeout(() => {
            eventSource.close();
            connect();
          }, delay);
        } else {
          console.error('[useSessionSync] Max reconnection attempts reached');
          eventSource.close();
        }
      };

      // Listen to all score/session event types
      Object.values(SessionEventType).forEach(eventType => {
        eventSource.addEventListener(eventType, handleEvent);
      });

      // Listen to turn-order toolkit events (Issue #4975)
      if (onTurnAdvanced) {
        eventSource.addEventListener('session:toolkit', (event: MessageEvent<string>) => {
          try {
            const payload = JSON.parse(event.data) as TurnAdvancedPayload;
            onTurnAdvanced(payload);
          } catch (err) {
            console.error('[useSessionSync] session:toolkit parse error:', err);
          }
        });
      }

      eventSourceRef.current = eventSource;
    } catch (err) {
      const errorObj = err instanceof Error ? err : new Error('Failed to create EventSource');
      setError(errorObj);
      setIsConnected(false);
      onError?.(errorObj);
    }
  }, [sessionId, apiBaseUrl, handleEvent, onError, onTurnAdvanced]);

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
