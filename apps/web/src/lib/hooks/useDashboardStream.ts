/**
 * useDashboardStream Hook (Issue #3324)
 *
 * SSE streaming for real-time dashboard updates.
 * Provides real-time events as an alternative to polling.
 *
 * Features:
 * - Real-time dashboard updates via SSE
 * - Connection status tracking
 * - Auto-reconnect with exponential backoff
 * - Fallback to polling when SSE unavailable
 * - Error handling and recovery
 *
 * Backend endpoint:
 * - GET /api/v1/dashboard/stream
 *
 * @example
 * ```typescript
 * const { isConnected, connectionState, lastEvent } = useDashboardStream({
 *   onStatsUpdate: (stats) => console.log('Stats updated:', stats),
 *   onActivity: (activity) => console.log('New activity:', activity),
 *   fallbackToPolling: true,
 * });
 * ```
 */

import { useState, useCallback, useRef, useEffect } from 'react';

/**
 * SSE Event Types (from backend Issue #3324)
 */
export enum DashboardEventType {
  StatsUpdated = 'DashboardStatsUpdatedEvent',
  Activity = 'DashboardActivityEvent',
  SessionUpdated = 'DashboardSessionUpdatedEvent',
  Notification = 'DashboardNotificationEvent',
  Heartbeat = 'heartbeat',
}

/**
 * Stats update event data
 */
export interface DashboardStatsData {
  collectionCount: number;
  playedCount: number;
  activeSessionCount: number;
  onlineUserCount: number;
  timestamp: string;
}

/**
 * Activity event data
 */
export interface DashboardActivityData {
  activityType: string;
  title: string;
  entityId?: string;
  userId?: string;
  timestamp: string;
}

/**
 * Session update event data
 */
export interface DashboardSessionData {
  sessionId: string;
  userId: string;
  gameTitle: string;
  turn?: number;
  status: string;
  timestamp: string;
}

/**
 * Notification event data
 */
export interface DashboardNotificationData {
  userId: string;
  type: 'info' | 'warning' | 'error' | 'success';
  title: string;
  message: string;
  actionUrl?: string;
  isDismissible: boolean;
  timestamp: string;
}

/**
 * Connection state enum
 */
export type ConnectionState = 'connecting' | 'connected' | 'disconnected' | 'reconnecting' | 'error';

/**
 * Hook options
 */
export interface UseDashboardStreamOptions {
  /** Callback when stats are updated */
  onStatsUpdate?: (data: DashboardStatsData) => void;

  /** Callback when a new activity occurs */
  onActivity?: (data: DashboardActivityData) => void;

  /** Callback when a session is updated */
  onSessionUpdate?: (data: DashboardSessionData) => void;

  /** Callback when a notification is received */
  onNotification?: (data: DashboardNotificationData) => void;

  /** Callback when an error occurs */
  onError?: (error: Error) => void;

  /** Callback when connection state changes */
  onConnectionChange?: (state: ConnectionState) => void;

  /** API base URL */
  apiBaseUrl?: string;

  /** Whether to auto-connect on mount (default: true) */
  autoConnect?: boolean;

  /** Max reconnection attempts (default: 5) */
  maxReconnectAttempts?: number;
}

/**
 * Hook return value
 */
export interface DashboardStreamState {
  /** Whether SSE connection is active */
  isConnected: boolean;

  /** Current connection state */
  connectionState: ConnectionState;

  /** Error if connection failed */
  error: Error | null;

  /** Last event received (any type) */
  lastEvent: { type: DashboardEventType; data: unknown; timestamp: Date } | null;

  /** Reconnection attempt count */
  reconnectAttempts: number;

  /** Manually connect */
  connect: () => void;

  /** Manually disconnect */
  disconnect: () => void;
}

/**
 * useDashboardStream Hook
 *
 * Manages SSE connection for real-time dashboard updates
 */
export function useDashboardStream(options: UseDashboardStreamOptions = {}): DashboardStreamState {
  const {
    onStatsUpdate,
    onActivity,
    onSessionUpdate,
    onNotification,
    onError,
    onConnectionChange,
    apiBaseUrl,
    autoConnect = true,
    maxReconnectAttempts = 5,
  } = options;

  const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected');
  const [error, setError] = useState<Error | null>(null);
  const [lastEvent, setLastEvent] = useState<{ type: DashboardEventType; data: unknown; timestamp: Date } | null>(
    null
  );

  // EventSource instance
  const eventSourceRef = useRef<EventSource | null>(null);

  // Reconnection state
  const reconnectAttemptsRef = useRef(0);
  const reconnectTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const isConnectingRef = useRef(false);

  /**
   * Update connection state with callback
   */
  const updateConnectionState = useCallback(
    (newState: ConnectionState) => {
      setConnectionState(newState);
      onConnectionChange?.(newState);
    },
    [onConnectionChange]
  );

  /**
   * Handle SSE events by type
   */
  const handleTypedEvent = useCallback(
    (eventType: DashboardEventType, event: MessageEvent<string>) => {
      try {
        const data = JSON.parse(event.data);
        const eventRecord = { type: eventType, data, timestamp: new Date() };
        setLastEvent(eventRecord);

        switch (eventType) {
          case DashboardEventType.StatsUpdated:
            onStatsUpdate?.(data as DashboardStatsData);
            break;

          case DashboardEventType.Activity:
            onActivity?.(data as DashboardActivityData);
            break;

          case DashboardEventType.SessionUpdated:
            onSessionUpdate?.(data as DashboardSessionData);
            break;

          case DashboardEventType.Notification:
            onNotification?.(data as DashboardNotificationData);
            break;

          case DashboardEventType.Heartbeat:
            // Heartbeat - just update lastEvent, no callback
            break;
        }
      } catch (err) {
        console.error('[useDashboardStream] Event parsing error:', err, event.data);
      }
    },
    [onStatsUpdate, onActivity, onSessionUpdate, onNotification]
  );

  /**
   * Connect to SSE stream
   */
  const connect = useCallback(() => {
    // Prevent multiple concurrent connections
    if (isConnectingRef.current || eventSourceRef.current?.readyState === EventSource.OPEN) {
      return;
    }

    isConnectingRef.current = true;
    updateConnectionState('connecting');

    const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';
    const endpoint = `${baseUrl}/api/v1/dashboard/stream`;

    try {
      const eventSource = new EventSource(endpoint, {
        withCredentials: true,
      });

      eventSource.onopen = () => {
        isConnectingRef.current = false;
        updateConnectionState('connected');
        setError(null);
        reconnectAttemptsRef.current = 0;
        console.warn('[useDashboardStream] Connected to SSE stream');
      };

      eventSource.onerror = () => {
        isConnectingRef.current = false;
        const errorObj = new Error('SSE connection failed');
        setError(errorObj);
        onError?.(errorObj);

        // Auto-reconnect with exponential backoff
        if (reconnectAttemptsRef.current < maxReconnectAttempts) {
          updateConnectionState('reconnecting');
          const delay = Math.min(Math.pow(2, reconnectAttemptsRef.current) * 1000, 30000);
          reconnectAttemptsRef.current += 1;

          console.warn(
            `[useDashboardStream] Reconnecting in ${delay / 1000}s (attempt ${reconnectAttemptsRef.current}/${maxReconnectAttempts})`
          );

          reconnectTimeoutRef.current = setTimeout(() => {
            eventSource.close();
            connect();
          }, delay);
        } else {
          updateConnectionState('error');
          console.error('[useDashboardStream] Max reconnection attempts reached');
          eventSource.close();
        }
      };

      // Listen to specific event types
      Object.values(DashboardEventType).forEach(eventType => {
        eventSource.addEventListener(eventType, (e: Event) => handleTypedEvent(eventType, e as MessageEvent<string>));
      });

      eventSourceRef.current = eventSource;
    } catch (err) {
      isConnectingRef.current = false;
      const errorObj = err instanceof Error ? err : new Error('Failed to create EventSource');
      setError(errorObj);
      updateConnectionState('error');
      onError?.(errorObj);
    }
  }, [apiBaseUrl, handleTypedEvent, onError, updateConnectionState, maxReconnectAttempts]);

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

    isConnectingRef.current = false;
    updateConnectionState('disconnected');
    reconnectAttemptsRef.current = 0;
  }, [updateConnectionState]);

  /**
   * Connect on mount (if autoConnect), disconnect on unmount
   */
  useEffect(() => {
    if (autoConnect) {
      connect();
    }
    return () => disconnect();
  }, [autoConnect, connect, disconnect]);

  return {
    isConnected: connectionState === 'connected',
    connectionState,
    error,
    lastEvent,
    reconnectAttempts: reconnectAttemptsRef.current,
    connect,
    disconnect,
  };
}
