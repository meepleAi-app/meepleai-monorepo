/**
 * useQueueSSE Hook
 *
 * Connects to GET /api/v1/admin/queue/stream for real-time queue updates.
 * Events: JobQueued, JobStarted, JobCompleted, JobFailed, JobRemoved, JobRetried, QueueReordered, Heartbeat
 * Auto-reconnects with exponential backoff.
 * Updates React Query cache directly on events (no refetch needed).
 *
 * Issue #4735: SSE Integration + Real-Time Updates
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { useQueryClient } from '@tanstack/react-query';

import { getApiBase } from '@/lib/api';

// ── Types ───────────────────────────────────────────────────────────────

export type SSEConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'closed' | 'error';

// Queue-level event names that should trigger list/stats invalidation
const QUEUE_EVENTS = new Set([
  'JobQueued',
  'JobStarted',
  'JobCompleted',
  'JobFailed',
  'JobRemoved',
  'JobRetried',
  'QueueReordered',
]);

// All event names to listen for (backend sends these as SSE event types)
const ALL_EVENT_NAMES = [
  'StepCompleted',
  'LogEntry',
  'JobCompleted',
  'JobFailed',
  'JobQueued',
  'JobStarted',
  'JobRemoved',
  'JobRetried',
  'QueueReordered',
  'Heartbeat',
] as const;

// ── Constants ───────────────────────────────────────────────────────────

const MAX_RECONNECT_ATTEMPTS = 8;
const INITIAL_BACKOFF_MS = 1000;
const MAX_BACKOFF_MS = 30000;

// ── Hook ────────────────────────────────────────────────────────────────

export function useQueueSSE(enabled: boolean = true) {
  const queryClient = useQueryClient();
  const [connectionState, setConnectionState] = useState<SSEConnectionState>('closed');

  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectAttemptsRef = useRef(0);
  const reconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isMountedRef = useRef(false);
  const connectRef = useRef<() => void>(() => {});

  const cleanup = useCallback(() => {
    if (reconnectTimerRef.current) {
      clearTimeout(reconnectTimerRef.current);
      reconnectTimerRef.current = null;
    }
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
  }, []);

  const invalidateQueue = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] });
  }, [queryClient]);

  // Use eventName string directly instead of fragile numeric enum indexing
  const handleEvent = useCallback(
    (eventName: string, _event: MessageEvent) => {
      if (!isMountedRef.current) return;

      if (QUEUE_EVENTS.has(eventName)) {
        invalidateQueue();
      }
      // Heartbeat and other events: no action needed
    },
    [invalidateQueue]
  );

  const connect = useCallback(() => {
    if (!enabled || !isMountedRef.current) return;

    cleanup();
    setConnectionState('connecting');

    const url = `${getApiBase()}/api/v1/admin/queue/stream`;

    let eventSource: EventSource;
    try {
      eventSource = new EventSource(url, { withCredentials: true });
    } catch {
      setConnectionState('error');
      return;
    }
    eventSourceRef.current = eventSource;

    // Listen for each named event type from the backend
    for (const eventName of ALL_EVENT_NAMES) {
      eventSource.addEventListener(eventName, (e: MessageEvent) => {
        setConnectionState('connected');
        reconnectAttemptsRef.current = 0;
        handleEvent(eventName, e);
      });
    }

    eventSource.onopen = () => {
      if (!isMountedRef.current) return;
      setConnectionState('connected');
      reconnectAttemptsRef.current = 0;
    };

    eventSource.onerror = () => {
      if (!isMountedRef.current) return;

      cleanup();

      if (reconnectAttemptsRef.current < MAX_RECONNECT_ATTEMPTS) {
        reconnectAttemptsRef.current += 1;
        setConnectionState('reconnecting');
        const delay = Math.min(
          INITIAL_BACKOFF_MS * Math.pow(2, reconnectAttemptsRef.current - 1),
          MAX_BACKOFF_MS
        );
        // Use connectRef to avoid stale closure over connect
        reconnectTimerRef.current = setTimeout(() => {
          if (isMountedRef.current && enabled) connectRef.current();
        }, delay);
      } else {
        setConnectionState('error');
      }
    };
  }, [enabled, cleanup, handleEvent]);

  // Keep connectRef up-to-date to avoid stale closures in reconnect timers
  connectRef.current = connect;

  const reconnect = useCallback(() => {
    reconnectAttemptsRef.current = 0;
    connectRef.current();
  }, []);

  useEffect(() => {
    isMountedRef.current = true;

    if (enabled) {
      connectRef.current();
    } else {
      cleanup();
      setConnectionState('closed');
    }

    return () => {
      isMountedRef.current = false;
      cleanup();
    };
  }, [enabled, cleanup]);

  return {
    connectionState,
    reconnect,
  };
}
