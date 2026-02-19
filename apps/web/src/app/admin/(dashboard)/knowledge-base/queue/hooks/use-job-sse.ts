/**
 * useJobSSE Hook
 *
 * Connects to GET /api/v1/admin/queue/{jobId}/stream for real-time job updates.
 * Events: StepCompleted, LogEntry, JobCompleted, JobFailed
 * Auto-reconnects, auto-closes on terminal events.
 * Updates job detail React Query cache directly.
 *
 * Issue #4735: SSE Integration + Real-Time Updates
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';

import { getApiBase } from '@/lib/api';

import type { SSEConnectionState } from './use-queue-sse';

// ── Types ───────────────────────────────────────────────────────────────

// Terminal event names that should close the stream
const TERMINAL_EVENTS = new Set(['JobCompleted', 'JobFailed']);

// All event names to listen for on a job stream
const JOB_EVENT_NAMES = [
  'StepCompleted',
  'LogEntry',
  'JobCompleted',
  'JobFailed',
  'Heartbeat',
] as const;

// ── Constants ───────────────────────────────────────────────────────────

const MAX_RECONNECT_ATTEMPTS = 5;
const INITIAL_BACKOFF_MS = 1000;
const MAX_BACKOFF_MS = 16000;
const DEBOUNCE_MS = 200;

// ── Hook ────────────────────────────────────────────────────────────────

export function useJobSSE(jobId: string | null) {
  const queryClient = useQueryClient();
  const [connectionState, setConnectionState] = useState<SSEConnectionState>('closed');

  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectAttemptsRef = useRef(0);
  const reconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const debounceTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isMountedRef = useRef(false);
  const isTerminatedRef = useRef(false);
  const pendingInvalidateRef = useRef(false);
  const connectRef = useRef<() => void>(() => {});

  const cleanup = useCallback(() => {
    if (reconnectTimerRef.current) {
      clearTimeout(reconnectTimerRef.current);
      reconnectTimerRef.current = null;
    }
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = null;
    }
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
  }, []);

  // Debounced invalidation to avoid rapid re-renders from progress events
  const invalidateJobDetail = useCallback(
    (immediate: boolean = false) => {
      if (!jobId) return;

      if (immediate) {
        if (debounceTimerRef.current) {
          clearTimeout(debounceTimerRef.current);
          debounceTimerRef.current = null;
        }
        pendingInvalidateRef.current = false;
        queryClient.invalidateQueries({ queryKey: ['admin', 'queue', 'detail', jobId] });
        queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] });
        return;
      }

      // Debounce rapid updates - invalidate both detail and list for currentStep updates
      pendingInvalidateRef.current = true;
      if (!debounceTimerRef.current) {
        debounceTimerRef.current = setTimeout(() => {
          debounceTimerRef.current = null;
          if (pendingInvalidateRef.current && isMountedRef.current && !isTerminatedRef.current) {
            pendingInvalidateRef.current = false;
            queryClient.invalidateQueries({ queryKey: ['admin', 'queue', 'detail', jobId] });
            queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] });
          }
        }, DEBOUNCE_MS);
      }
    },
    [jobId, queryClient],
  );

  // Use eventName string directly instead of fragile numeric enum indexing
  const handleEvent = useCallback(
    (eventName: string, _event: MessageEvent) => {
      if (!isMountedRef.current) return;

      if (TERMINAL_EVENTS.has(eventName)) {
        // Terminal event: invalidate immediately and close
        isTerminatedRef.current = true;
        invalidateJobDetail(true);
        cleanup();
        setConnectionState('closed');
        return;
      }

      // Non-terminal: debounced invalidation
      invalidateJobDetail(false);
    },
    [invalidateJobDetail, cleanup],
  );

  const connect = useCallback(() => {
    if (!jobId || !isMountedRef.current) return;

    cleanup();
    isTerminatedRef.current = false;
    setConnectionState('connecting');

    const url = `${getApiBase()}/api/v1/admin/queue/${jobId}/stream`;

    let eventSource: EventSource;
    try {
      eventSource = new EventSource(url, { withCredentials: true });
    } catch {
      setConnectionState('error');
      return;
    }
    eventSourceRef.current = eventSource;

    for (const eventName of JOB_EVENT_NAMES) {
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
          MAX_BACKOFF_MS,
        );
        // Use connectRef to avoid stale closure over connect
        reconnectTimerRef.current = setTimeout(() => {
          if (isMountedRef.current && jobId) connectRef.current();
        }, delay);
      } else {
        setConnectionState('error');
      }
    };
  }, [jobId, cleanup, handleEvent]);

  // Keep connectRef up-to-date to avoid stale closures in reconnect timers
  connectRef.current = connect;

  useEffect(() => {
    isMountedRef.current = true;

    if (jobId) {
      connectRef.current();
    } else {
      cleanup();
      setConnectionState('closed');
    }

    return () => {
      isMountedRef.current = false;
      cleanup();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- connect called via stable ref to avoid infinite loop
  }, [jobId, cleanup]);

  return { connectionState };
}
