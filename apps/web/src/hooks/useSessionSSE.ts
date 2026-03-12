'use client';

/**
 * useSessionSSE Hook
 *
 * Real-time activity feed updates via Server-Sent Events.
 * Connects to the session events stream and dispatches parsed events
 * to the session store.
 *
 * Features:
 * - SSE connection to /api/v1/sessions/{sessionId}/events/stream
 * - Deduplication via Set<string> of seen event IDs
 * - Exponential backoff reconnection (1s → 2s → 4s → 8s → 16s max)
 * - 5-minute offline threshold: stops reconnecting after 5 min disconnected
 * - Cleanup on unmount
 */

import { useEffect, useRef, useCallback } from 'react';

import type { ActivityEvent } from '@/store/session';
import { useSessionStore } from '@/store/session';

// ============================================================================
// Constants
// ============================================================================

const INITIAL_BACKOFF_MS = 1000;
const MAX_BACKOFF_MS = 16000;
const OFFLINE_THRESHOLD_MS = 5 * 60 * 1000; // 5 minutes

// ============================================================================
// Hook
// ============================================================================

export function useSessionSSE(sessionId: string | null): void {
  const addEvent = useSessionStore(s => s.addEvent);
  const addEventRef = useRef(addEvent);

  useEffect(() => {
    addEventRef.current = addEvent;
  }, [addEvent]);

  const seenIdsRef = useRef(new Set<string>());
  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const backoffRef = useRef(INITIAL_BACKOFF_MS);
  const disconnectedAtRef = useRef<number | null>(null);
  const isMountedRef = useRef(false);

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
    if (!isMountedRef.current || !sessionId) return;

    cleanup();

    try {
      const endpoint = `/api/v1/sessions/${sessionId}/events/stream`;
      const eventSource = new EventSource(endpoint, { withCredentials: true });

      eventSourceRef.current = eventSource;

      eventSource.onopen = () => {
        if (!isMountedRef.current) return;
        backoffRef.current = INITIAL_BACKOFF_MS;
        disconnectedAtRef.current = null;
      };

      eventSource.onmessage = (event: MessageEvent<string>) => {
        if (!isMountedRef.current) return;

        try {
          const parsed = JSON.parse(event.data) as ActivityEvent;

          if (seenIdsRef.current.has(parsed.id)) return;
          seenIdsRef.current.add(parsed.id);

          addEventRef.current(parsed);
        } catch {
          // Ignore malformed messages
        }
      };

      eventSource.onerror = () => {
        if (!isMountedRef.current) return;

        eventSource.close();
        eventSourceRef.current = null;

        // Track when we first went offline
        if (disconnectedAtRef.current === null) {
          disconnectedAtRef.current = Date.now();
        }

        // Stop reconnecting after 5-minute offline threshold
        const offlineDuration = Date.now() - disconnectedAtRef.current;
        if (offlineDuration >= OFFLINE_THRESHOLD_MS) {
          return;
        }

        // Exponential backoff reconnection
        const delay = backoffRef.current;
        backoffRef.current = Math.min(backoffRef.current * 2, MAX_BACKOFF_MS);

        reconnectTimerRef.current = setTimeout(() => {
          if (isMountedRef.current) {
            connectRef.current();
          }
        }, delay);
      };
    } catch {
      // EventSource not supported or other error — silently degrade
    }
  }, [sessionId, cleanup]);

  // Ref to avoid stale closure over connect in reconnect timers
  const connectRef = useRef(connect);
  useEffect(() => {
    connectRef.current = connect;
  }, [connect]);

  useEffect(() => {
    isMountedRef.current = true;
    seenIdsRef.current = new Set(); // Clear dedup set on session change

    if (sessionId) {
      connect();
    }

    return () => {
      isMountedRef.current = false;
      cleanup();
    };
  }, [sessionId, connect, cleanup]);
}
