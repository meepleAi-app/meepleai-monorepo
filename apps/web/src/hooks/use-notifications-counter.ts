/**
 * useNotificationsCounter Hook
 *
 * Connects to GET /api/v1/notifications/stream (SSE) for real-time unread
 * notification count updates. Initial count fetched via
 * GET /api/v1/notifications/unread-count on mount.
 *
 * Pattern reference: use-job-sse.ts (Issue #4735).
 * Asse B (#1897) WP6 T6 DEC-3.
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { getApiBase } from '@/lib/api';

// ── Types ───────────────────────────────────────────────────────────────

export type CounterConnectionState =
  | 'idle'
  | 'fetching'
  | 'connecting'
  | 'open'
  | 'reconnecting'
  | 'closed';

interface UseNotificationsCounterResult {
  count: number;
  connectionState: CounterConnectionState;
  error: Error | null;
}

interface UseNotificationsCounterOptions {
  /**
   * Whether to actually start the connection. When false (e.g., user not
   * authenticated), the hook is a no-op and returns count=0.
   * @default true
   */
  enabled?: boolean;
}

// ── Constants ───────────────────────────────────────────────────────────

const MAX_RECONNECT_ATTEMPTS = 5;
const INITIAL_BACKOFF_MS = 1000;
const MAX_BACKOFF_MS = 16000;

// ── Hook ────────────────────────────────────────────────────────────────

export function useNotificationsCounter(
  options: UseNotificationsCounterOptions = {}
): UseNotificationsCounterResult {
  const { enabled = true } = options;

  const [count, setCount] = useState(0);
  const [connectionState, setConnectionState] = useState<CounterConnectionState>('idle');
  const [error, setError] = useState<Error | null>(null);

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

  const fetchInitialCount = useCallback(async (signal: AbortSignal): Promise<number | null> => {
    setConnectionState('fetching');
    try {
      const apiBase = getApiBase();
      const res = await fetch(`${apiBase}/api/v1/notifications/unread-count`, {
        credentials: 'include',
        signal,
      });
      if (!res.ok) {
        if (res.status === 401) {
          // Unauthenticated — silent no-op
          if (isMountedRef.current) setConnectionState('closed');
          return null;
        }
        throw new Error(`Unread count fetch failed: ${res.status}`);
      }
      const data = (await res.json()) as { count: number };
      if (isMountedRef.current) {
        setCount(data.count ?? 0);
      }
      return data.count ?? 0;
    } catch (err) {
      if ((err as Error).name === 'AbortError') return null;
      if (isMountedRef.current) {
        setError(err as Error);
      }
      return null;
    }
  }, []);

  const connect = useCallback(() => {
    if (!isMountedRef.current) return;

    cleanup();

    setConnectionState('connecting');
    const apiBase = getApiBase();
    const es = new EventSource(`${apiBase}/api/v1/notifications/stream`, {
      withCredentials: true,
    });
    eventSourceRef.current = es;

    es.onopen = () => {
      if (!isMountedRef.current) return;
      setConnectionState('open');
      reconnectAttemptsRef.current = 0;
    };

    // New notification event → increment counter
    es.addEventListener('notification', () => {
      if (!isMountedRef.current) return;
      setCount(c => c + 1);
    });

    es.onerror = () => {
      if (!isMountedRef.current) return;

      es.close();
      eventSourceRef.current = null;

      const attempts = reconnectAttemptsRef.current;
      if (attempts >= MAX_RECONNECT_ATTEMPTS) {
        setConnectionState('closed');
        setError(new Error('SSE disconnected after max reconnect attempts'));
        return;
      }

      const backoff = Math.min(INITIAL_BACKOFF_MS * Math.pow(2, attempts), MAX_BACKOFF_MS);

      setConnectionState('reconnecting');
      reconnectAttemptsRef.current = attempts + 1;

      reconnectTimerRef.current = setTimeout(() => {
        connectRef.current();
      }, backoff);
    };
  }, [cleanup]);

  // Keep connect ref in sync to avoid stale closures in reconnect timers
  useEffect(() => {
    connectRef.current = connect;
  }, [connect]);

  useEffect(() => {
    isMountedRef.current = true;

    if (!enabled) {
      setConnectionState('closed');
      setCount(0);
      return () => {
        isMountedRef.current = false;
      };
    }

    const abortController = new AbortController();

    const init = async () => {
      const initialCount = await fetchInitialCount(abortController.signal);
      // Only connect SSE if initial fetch succeeded (skip if 401 returned null)
      if (initialCount !== null && isMountedRef.current) {
        connect();
      }
    };

    void init();

    return () => {
      isMountedRef.current = false;
      abortController.abort();
      cleanup();
    };
  }, [enabled, fetchInitialCount, connect, cleanup]);

  return { count, connectionState, error };
}
