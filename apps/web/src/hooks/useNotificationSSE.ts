/**
 * useNotificationSSE Hook (Issue #4414)
 *
 * Real-time notification updates via Server-Sent Events.
 * Follows usePdfStatus pattern: SSE with exponential backoff reconnection.
 *
 * Features:
 * - SSE connection to /api/v1/notifications/stream
 * - Auto-reconnect with exponential backoff (1s, 2s, 4s, 8s, 16s, max 30s)
 * - Adds new notifications to store via addNotification()
 * - Updates unread count badge in real-time
 * - Graceful degradation: silently disables if endpoint unavailable
 * - Cleanup on unmount
 */

import { useEffect, useRef, useCallback } from 'react';

import type { NotificationDto } from '@/lib/api';
import { useNotificationStore } from '@/stores/notification/store';

// ============================================================================
// Constants
// ============================================================================

const MAX_RECONNECT_ATTEMPTS = 5;
const INITIAL_BACKOFF_DELAY = 1000; // 1 second
const MAX_BACKOFF_DELAY = 30000; // 30 seconds
const SSE_ENDPOINT = '/api/v1/notifications/stream';

// ============================================================================
// Helper
// ============================================================================

function calculateBackoffDelay(attempt: number): number {
  const delay = INITIAL_BACKOFF_DELAY * Math.pow(2, attempt);
  return Math.min(delay, MAX_BACKOFF_DELAY);
}

// ============================================================================
// Hook
// ============================================================================

export interface UseNotificationSSEOptions {
  /** Enable SSE connection (default: true) */
  enabled?: boolean;
}

export function useNotificationSSE(options: UseNotificationSSEOptions = {}) {
  const { enabled = true } = options;

  const addNotification = useNotificationStore(state => state.addNotification);
  const fetchUnreadCount = useNotificationStore(state => state.fetchUnreadCount);

  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectAttemptsRef = useRef(0);
  const isMountedRef = useRef(true);
  const reconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const addNotificationRef = useRef(addNotification);
  const fetchUnreadCountRef = useRef(fetchUnreadCount);

  useEffect(() => {
    addNotificationRef.current = addNotification;
    fetchUnreadCountRef.current = fetchUnreadCount;
  }, [addNotification, fetchUnreadCount]);

  const cleanup = useCallback(() => {
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
    if (reconnectTimerRef.current) {
      clearTimeout(reconnectTimerRef.current);
      reconnectTimerRef.current = null;
    }
  }, []);

  const connect = useCallback(() => {
    if (!isMountedRef.current) return;

    cleanup();

    try {
      const eventSource = new EventSource(SSE_ENDPOINT, {
        withCredentials: true,
      });

      eventSourceRef.current = eventSource;

      eventSource.onopen = () => {
        if (!isMountedRef.current) return;
        reconnectAttemptsRef.current = 0;
      };

      eventSource.onmessage = event => {
        if (!isMountedRef.current) return;

        try {
          const notification = JSON.parse(event.data) as NotificationDto;
          addNotificationRef.current(notification);
        } catch {
          // Ignore malformed messages
        }
      };

      eventSource.onerror = () => {
        if (!isMountedRef.current) return;

        eventSource.close();
        eventSourceRef.current = null;

        if (reconnectAttemptsRef.current < MAX_RECONNECT_ATTEMPTS) {
          const delay = calculateBackoffDelay(reconnectAttemptsRef.current);
          reconnectAttemptsRef.current += 1;

          reconnectTimerRef.current = setTimeout(() => {
            if (isMountedRef.current) {
              connect();
            }
          }, delay);
        }
        // After max attempts, silently stop - dropdown still fetches on open
      };
    } catch {
      // EventSource not supported or other error - silently degrade
    }
  }, [cleanup]);

  useEffect(() => {
    isMountedRef.current = true;

    if (enabled) {
      connect();
    }

    return () => {
      isMountedRef.current = false;
      cleanup();
    };
  }, [enabled, connect, cleanup]);
}
