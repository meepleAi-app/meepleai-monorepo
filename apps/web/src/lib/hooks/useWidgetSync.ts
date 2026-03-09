'use client';

/**
 * useWidgetSync Hook (P2-3 — Widget State Synchronization)
 *
 * Subscribes to SSE `session:toolkit` events filtered by widget type and
 * provides a debounced `broadcastState` function to push local changes
 * to the server.
 *
 * Features:
 * - Real-time widget state sync via V2 SSE stream (typed events)
 * - Debounced outgoing state broadcasts (configurable, default 300ms)
 * - Echo prevention — ignores remote events matching the last local broadcast
 * - Connection status tracking
 *
 * Backend endpoints:
 * - GET  /api/v1/game-sessions/{sessionId}/stream/v2  (SSE, typed events)
 * - PATCH /api/v1/game-sessions/{sessionId}/toolkit-state/{widgetType}?toolkitId={toolkitId}
 *
 * @example
 * ```typescript
 * const { broadcastState, isConnected } = useWidgetSync({
 *   sessionId: 'abc-123',
 *   toolkitId: 'toolkit-456',
 *   widgetType: 'ScoreTracker',
 *   onRemoteUpdate: (stateJson, updatedBy) => {
 *     setLocalState(JSON.parse(stateJson));
 *   },
 * });
 *
 * // When local state changes:
 * broadcastState(JSON.stringify(newState));
 * ```
 */

import { useCallback, useEffect, useRef, useState } from 'react';

// ============ Types ============

interface WidgetStateEvent {
  sessionId: string;
  toolkitId: string;
  widgetType: string;
  stateJson: string;
  updatedByUserId: string;
  timestamp: string;
}

export interface UseWidgetSyncOptions {
  /** Game session ID */
  sessionId: string | undefined;

  /** Toolkit ID for the PATCH endpoint */
  toolkitId: string | undefined;

  /** Widget type to filter incoming SSE events */
  widgetType: string;

  /** Called when a remote state update arrives for this widget type */
  onRemoteUpdate: (stateJson: string, updatedBy: string) => void;

  /** Debounce interval for outgoing broadcasts (ms). Default: 300 */
  debounceMs?: number;

  /** Whether the hook is active. Default: true */
  enabled?: boolean;
}

export interface UseWidgetSyncReturn {
  /** Push local state to the server (debounced) */
  broadcastState: (stateJson: string) => void;

  /** Whether the SSE connection is open */
  isConnected: boolean;
}

// ============ Hook ============

export function useWidgetSync({
  sessionId,
  toolkitId,
  widgetType,
  onRemoteUpdate,
  debounceMs = 300,
  enabled = true,
}: UseWidgetSyncOptions): UseWidgetSyncReturn {
  const [isConnected, setIsConnected] = useState(false);

  // Refs for echo prevention and debounce
  const lastBroadcastRef = useRef<string | null>(null);
  const debounceTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const eventSourceRef = useRef<EventSource | null>(null);

  // Store latest callback in ref to avoid stale closures and re-triggering the effect
  const onRemoteUpdateRef = useRef(onRemoteUpdate);
  onRemoteUpdateRef.current = onRemoteUpdate;

  // ---- SSE subscription ----
  useEffect(() => {
    if (!enabled || !sessionId) return;

    const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
    const url = `${baseUrl}/api/v1/game-sessions/${sessionId}/stream/v2`;
    const es = new EventSource(url, { withCredentials: true });
    eventSourceRef.current = es;

    es.onopen = () => {
      setIsConnected(true);
    };

    es.onerror = () => {
      setIsConnected(false);
      // EventSource auto-reconnects by default; we track status only
    };

    // Listen for typed session:toolkit events
    es.addEventListener('session:toolkit', ((event: MessageEvent) => {
      try {
        const data: WidgetStateEvent = JSON.parse(event.data);

        // Filter by widget type
        if (data.widgetType !== widgetType) return;

        // Echo prevention: skip if this matches our last broadcast
        if (data.stateJson === lastBroadcastRef.current) {
          lastBroadcastRef.current = null;
          return;
        }

        onRemoteUpdateRef.current(data.stateJson, data.updatedByUserId);
      } catch {
        // Silently ignore parse errors
      }
    }) as EventListener);

    return () => {
      es.close();
      eventSourceRef.current = null;
      setIsConnected(false);
    };
  }, [sessionId, widgetType, enabled]);

  // ---- Broadcast (debounced PATCH) ----
  const broadcastState = useCallback(
    (stateJson: string) => {
      if (!enabled || !sessionId || !toolkitId) return;

      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
      }

      debounceTimerRef.current = setTimeout(async () => {
        lastBroadcastRef.current = stateJson;
        try {
          const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
          await fetch(
            `${baseUrl}/api/v1/game-sessions/${sessionId}/toolkit-state/${widgetType}?toolkitId=${toolkitId}`,
            {
              method: 'PATCH',
              credentials: 'include',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ stateJson }),
            }
          );
        } catch {
          // Silently fail — next broadcast will carry the latest state
        }
      }, debounceMs);
    },
    [sessionId, toolkitId, widgetType, enabled, debounceMs]
  );

  // ---- Cleanup debounce timer on unmount ----
  useEffect(() => {
    return () => {
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
      }
    };
  }, []);

  return { broadcastState, isConnected };
}
