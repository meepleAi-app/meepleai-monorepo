/**
 * useSessionStream - SSE Client for Real-time Game Sessions
 * Issue #4767 - SSE Client + Player/Spectator Mode UI
 *
 * React hook wrapping EventSource for session-specific SSE with:
 * - Auto-reconnection with exponential backoff
 * - Typed event routing (session:state, session:player, session:score, etc.)
 * - Connection status tracking
 * - Zustand store integration
 */

import { useEffect, useRef, useCallback, useState } from 'react';

// ============ SSE Event Types ============

export type SessionSSEEventType =
  | 'session:state'
  | 'session:player'
  | 'session:score'
  | 'session:conflict'
  | 'session:toolkit'
  | 'session:chat';

export type ConnectionStatus =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'failed';

export interface SessionSSEEvent<T = unknown> {
  type: SessionSSEEventType;
  data: T;
  timestamp: string;
}

// Typed event payloads
export interface PlayerJoinedPayload {
  sessionId: string;
  participantId: string;
  displayName: string;
  isOwner: boolean;
  joinOrder: number;
}

export interface PlayerKickedPayload {
  sessionId: string;
  participantId: string;
  displayName: string;
  kickedBy: string;
}

export interface PlayerReadyPayload {
  sessionId: string;
  participantId: string;
  isReady: boolean;
}

export interface RoleChangedPayload {
  sessionId: string;
  participantId: string;
  displayName: string;
  previousRole: string;
  newRole: string;
  changedBy: string;
}

export interface ScoreUpdatedPayload {
  sessionId: string;
  participantId: string;
  scoreEntryId: string;
  newScore: number;
}

export interface SessionStatePayload {
  sessionId: string;
  status: string;
}

// ============ Hook Options ============

export interface UseSessionStreamOptions {
  onPlayerJoined?: (payload: PlayerJoinedPayload) => void;
  onPlayerKicked?: (payload: PlayerKickedPayload) => void;
  onPlayerReady?: (payload: PlayerReadyPayload) => void;
  onRoleChanged?: (payload: RoleChangedPayload) => void;
  onScoreUpdated?: (payload: ScoreUpdatedPayload) => void;
  onSessionStateChanged?: (payload: SessionStatePayload) => void;
  onToolkitEvent?: (payload: unknown) => void;
  onChatMessage?: (payload: unknown) => void;
  onError?: (error: Error) => void;
  enabled?: boolean;
  maxReconnectAttempts?: number;
}

export interface UseSessionStreamReturn {
  connectionStatus: ConnectionStatus;
  reconnectCount: number;
  disconnect: () => void;
  reconnect: () => void;
}

// ============ Hook Implementation ============

export function useSessionStream(
  sessionId: string | null,
  options: UseSessionStreamOptions = {}
): UseSessionStreamReturn {
  const { enabled = true, maxReconnectAttempts = 5 } = options;

  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>('disconnected');
  const [reconnectCount, setReconnectCount] = useState(0);

  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const reconnectAttemptsRef = useRef(0);

  // Store latest callbacks and config in refs to avoid stale closures
  const callbacksRef = useRef(options);
  callbacksRef.current = options;
  const maxReconnectAttemptsRef = useRef(maxReconnectAttempts);
  maxReconnectAttemptsRef.current = maxReconnectAttempts;

  const handleMessage = useCallback((event: MessageEvent) => {
    try {
      const parsed: SessionSSEEvent = JSON.parse(event.data);
      const cbs = callbacksRef.current;

      switch (parsed.type) {
        case 'session:player': {
          const data = parsed.data as Record<string, unknown>;
          // Route to specific player events based on data shape
          if ('isReady' in data) {
            cbs.onPlayerReady?.(data as unknown as PlayerReadyPayload);
          } else if ('kickedBy' in data) {
            cbs.onPlayerKicked?.(data as unknown as PlayerKickedPayload);
          } else if ('previousRole' in data) {
            cbs.onRoleChanged?.(data as unknown as RoleChangedPayload);
          } else if ('joinOrder' in data) {
            cbs.onPlayerJoined?.(data as unknown as PlayerJoinedPayload);
          }
          break;
        }
        case 'session:score':
          cbs.onScoreUpdated?.(parsed.data as ScoreUpdatedPayload);
          break;
        case 'session:state':
          cbs.onSessionStateChanged?.(parsed.data as SessionStatePayload);
          break;
        case 'session:toolkit':
          cbs.onToolkitEvent?.(parsed.data);
          break;
        case 'session:chat':
          cbs.onChatMessage?.(parsed.data);
          break;
        case 'session:conflict':
          // Conflict events handled via state update
          cbs.onSessionStateChanged?.(parsed.data as SessionStatePayload);
          break;
      }
    } catch (err) {
      console.error('[useSessionStream] Failed to parse SSE event:', err);
    }
  }, []);

  // Use ref for connect to prevent useEffect re-triggering on callback changes
  const connectRef = useRef<() => void>(() => {});

  const doConnect = useCallback(() => {
    if (!sessionId || !enabled) return;

    // Cleanup existing connection
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
    }

    setConnectionStatus('connecting');

    const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
    const url = `${baseUrl}/api/v1/game-sessions/${sessionId}/stream`;

    try {
      const es = new EventSource(url, { withCredentials: true });
      eventSourceRef.current = es;

      es.onopen = () => {
        setConnectionStatus('connected');
        setReconnectCount(0);
        reconnectAttemptsRef.current = 0;
      };

      es.onmessage = handleMessage;

      // Listen for typed events
      es.addEventListener('session:state', handleMessage as EventListener);
      es.addEventListener('session:player', handleMessage as EventListener);
      es.addEventListener('session:score', handleMessage as EventListener);
      es.addEventListener('session:toolkit', handleMessage as EventListener);
      es.addEventListener('session:chat', handleMessage as EventListener);
      es.addEventListener('session:conflict', handleMessage as EventListener);

      es.onerror = () => {
        if (reconnectAttemptsRef.current < maxReconnectAttemptsRef.current) {
          setConnectionStatus('reconnecting');
          const delay = Math.min(Math.pow(2, reconnectAttemptsRef.current) * 1000, 30000);
          reconnectAttemptsRef.current += 1;
          setReconnectCount(reconnectAttemptsRef.current);

          reconnectTimeoutRef.current = setTimeout(() => {
            es.close();
            connectRef.current();
          }, delay);
        } else {
          setConnectionStatus('failed');
          es.close();
          eventSourceRef.current = null;
          callbacksRef.current.onError?.(new Error('SSE connection failed after max retries'));
        }
      };
    } catch (err) {
      setConnectionStatus('failed');
      callbacksRef.current.onError?.(
        err instanceof Error ? err : new Error('Failed to create EventSource')
      );
    }
  }, [sessionId, enabled, handleMessage]);

  connectRef.current = doConnect;

  const disconnect = useCallback(() => {
    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
    reconnectAttemptsRef.current = 0;
    setConnectionStatus('disconnected');
    setReconnectCount(0);
  }, []);

  const reconnect = useCallback(() => {
    disconnect();
    reconnectAttemptsRef.current = 0;
    connectRef.current();
  }, [disconnect]);

  // Connect/disconnect on sessionId or enabled changes only
  useEffect(() => {
    if (sessionId && enabled) {
      connectRef.current();
    } else {
      disconnect();
    }

    return () => {
      disconnect();
    };
  }, [sessionId, enabled, disconnect]);

  return {
    connectionStatus,
    reconnectCount,
    disconnect,
    reconnect,
  };
}
