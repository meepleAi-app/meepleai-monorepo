'use client';

/**
 * useToolboxSync Hook
 *
 * Real-time SSE sync for toolbox events (card draws, phase advances, etc.).
 * Connects to the toolbox events stream and dispatches parsed events
 * to the toolbox store.
 *
 * Features:
 * - SSE connection to /api/v1/toolboxes/{toolboxId}/events
 * - Auto-reconnect with exponential backoff (1s -> 30s, max 5 retries)
 * - Skips connection when toolboxStore.isOffline is true
 * - Cleanup on unmount
 *
 * Epic #412 — Game Toolbox.
 */

import { useCallback, useEffect, useRef } from 'react';

import { useToolboxStore } from '@/lib/stores/toolboxStore';

// ============================================================================
// Constants
// ============================================================================

const INITIAL_BACKOFF_MS = 1_000;
const MAX_BACKOFF_MS = 30_000;
const MAX_RETRIES = 5;

// ============================================================================
// Types
// ============================================================================

interface ToolboxSSEvent {
  type: string;
  data: unknown;
}

// ============================================================================
// Hook
// ============================================================================

export function useToolboxSync(toolboxId: string | undefined, enabled: boolean = true): void {
  const handleToolboxEvent = useToolboxStore(s => s.handleToolboxEvent);
  const isOffline = useToolboxStore(s => s.isOffline);

  // Keep latest callback in ref to avoid stale closures
  const handleEventRef = useRef(handleToolboxEvent);
  useEffect(() => {
    handleEventRef.current = handleToolboxEvent;
  }, [handleToolboxEvent]);

  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const backoffRef = useRef(INITIAL_BACKOFF_MS);
  const retryCountRef = useRef(0);
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
    if (!isMountedRef.current || !toolboxId) return;

    cleanup();

    try {
      const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
      const url = `${baseUrl}/api/v1/toolboxes/${toolboxId}/events`;
      const es = new EventSource(url, { withCredentials: true });

      eventSourceRef.current = es;

      es.onopen = () => {
        if (!isMountedRef.current) return;
        // Reset backoff and retry count on successful connection
        backoffRef.current = INITIAL_BACKOFF_MS;
        retryCountRef.current = 0;
      };

      es.onmessage = (event: MessageEvent<string>) => {
        if (!isMountedRef.current) return;

        try {
          const parsed: ToolboxSSEvent = JSON.parse(event.data);
          handleEventRef.current(parsed);
        } catch {
          // Ignore malformed messages
        }
      };

      es.onerror = () => {
        if (!isMountedRef.current) return;

        es.close();
        eventSourceRef.current = null;

        // Stop reconnecting after max retries
        if (retryCountRef.current >= MAX_RETRIES) {
          return;
        }

        retryCountRef.current += 1;
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
  }, [toolboxId, cleanup]);

  // Ref to avoid stale closure in reconnect timers
  const connectRef = useRef(connect);
  useEffect(() => {
    connectRef.current = connect;
  }, [connect]);

  useEffect(() => {
    isMountedRef.current = true;

    // Skip SSE when disabled or offline
    if (!enabled || isOffline || !toolboxId) {
      cleanup();
      return;
    }

    connect();

    return () => {
      isMountedRef.current = false;
      cleanup();
    };
  }, [toolboxId, enabled, isOffline, connect, cleanup]);
}
