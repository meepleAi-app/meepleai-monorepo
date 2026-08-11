/**
 * useWhiteboardTool Hook (Issue #4977)
 *
 * Manages whiteboard state persistence, API calls, and real-time SSE updates.
 *
 * Features:
 * - Load whiteboard state from server
 * - Save strokes (debounced 500ms)
 * - Save structured layer (tokens, grid settings) – debounced 500ms
 * - Clear the whiteboard
 * - Real-time sync via native SSE stream (session:whiteboard events)
 * - Exposes applySSEEvent for manual SSE integration
 *
 * Backend endpoints (wired when BE is available):
 * - GET  /api/v1/live-sessions/{sessionId}/whiteboard
 * - POST /api/v1/live-sessions/{sessionId}/whiteboard/strokes
 * - PUT  /api/v1/live-sessions/{sessionId}/whiteboard/structured
 * - POST /api/v1/live-sessions/{sessionId}/whiteboard/clear
 *
 * @example
 * ```typescript
 * const { whiteboardState, saveStrokes, clear } = useWhiteboardTool({ sessionId });
 * ```
 */

import { useState, useCallback, useEffect, useRef } from 'react';

import type {
  WhiteboardState,
  WhiteboardSSEEvent,
  Stroke,
  WhiteboardToken,
  GridSize,
  WhiteboardMode,
} from '@/components/session/types';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface UseWhiteboardToolOptions {
  /** Session ID */
  sessionId: string;
  /** Optional API base URL */
  apiBaseUrl?: string;
}

export interface WhiteboardHookState {
  /** Current whiteboard state (null when not yet loaded) */
  whiteboardState: WhiteboardState | null;
  /** True while initially loading */
  isLoading: boolean;
  /** True while a save is in flight */
  isPending: boolean;
  /** Error message if last operation failed */
  error: string | null;
  /** Save strokes (debounced 500ms) */
  saveStrokes: (strokes: Stroke[]) => void;
  /** Save structured layer (debounced 500ms) */
  saveStructured: (
    tokens: WhiteboardToken[],
    gridSize: GridSize,
    showGrid: boolean,
    mode: WhiteboardMode
  ) => void;
  /** Clear the whiteboard */
  clear: () => Promise<void>;
  /** Apply a SSE whiteboard event from the session stream */
  applySSEEvent: (event: WhiteboardSSEEvent) => void;
}

// ── Hook ──────────────────────────────────────────────────────────────────────

export function useWhiteboardTool({
  sessionId,
  apiBaseUrl,
}: UseWhiteboardToolOptions): WhiteboardHookState {
  const [whiteboardState, setWhiteboardState] = useState<WhiteboardState | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isPending, setIsPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const baseUrl = apiBaseUrl ?? process.env.NEXT_PUBLIC_API_BASE ?? '';

  // Debounce timer refs
  const strokesTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const structuredTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // ── Load ─────────────────────────────────────────────────────────────────────

  const loadWhiteboard = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await fetch(`${baseUrl}/api/v1/live-sessions/${sessionId}/whiteboard`, {
        credentials: 'include',
      });
      if (res.status === 404) {
        // Not yet created – initialize with defaults
        setWhiteboardState({
          strokes: [],
          tokens: [],
          gridSize: '4x4',
          showGrid: true,
          mode: 'both',
        });
        return;
      }
      if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      const data: WhiteboardState = await res.json();
      setWhiteboardState(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load whiteboard');
      // Fallback to empty state so the component is usable offline
      setWhiteboardState({
        strokes: [],
        tokens: [],
        gridSize: '4x4',
        showGrid: true,
        mode: 'both',
      });
    } finally {
      setIsLoading(false);
    }
  }, [sessionId, baseUrl]);

  // ── Save strokes (debounced) ──────────────────────────────────────────────────

  const saveStrokes = useCallback(
    (strokes: Stroke[]) => {
      if (strokesTimerRef.current) clearTimeout(strokesTimerRef.current);
      strokesTimerRef.current = setTimeout(async () => {
        setIsPending(true);
        try {
          const res = await fetch(
            `${baseUrl}/api/v1/live-sessions/${sessionId}/whiteboard/strokes`,
            {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              credentials: 'include',
              body: JSON.stringify({ strokes }),
            }
          );
          if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
          setWhiteboardState(prev => (prev ? { ...prev, strokes } : prev));
        } catch (err) {
          setError(err instanceof Error ? err.message : 'Failed to save strokes');
        } finally {
          setIsPending(false);
        }
      }, 500);
    },
    [sessionId, baseUrl]
  );

  // ── Save structured layer (debounced) ────────────────────────────────────────

  const saveStructured = useCallback(
    (tokens: WhiteboardToken[], gridSize: GridSize, showGrid: boolean, mode: WhiteboardMode) => {
      if (structuredTimerRef.current) clearTimeout(structuredTimerRef.current);
      structuredTimerRef.current = setTimeout(async () => {
        setIsPending(true);
        try {
          const res = await fetch(
            `${baseUrl}/api/v1/live-sessions/${sessionId}/whiteboard/structured`,
            {
              method: 'PUT',
              headers: { 'Content-Type': 'application/json' },
              credentials: 'include',
              body: JSON.stringify({ tokens, gridSize, showGrid, mode }),
            }
          );
          if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
          setWhiteboardState(prev => (prev ? { ...prev, tokens, gridSize, showGrid, mode } : prev));
        } catch (err) {
          setError(err instanceof Error ? err.message : 'Failed to save whiteboard settings');
        } finally {
          setIsPending(false);
        }
      }, 500);
    },
    [sessionId, baseUrl]
  );

  // ── Clear ─────────────────────────────────────────────────────────────────────

  const clear = useCallback(async () => {
    setIsPending(true);
    setError(null);
    try {
      const res = await fetch(`${baseUrl}/api/v1/live-sessions/${sessionId}/whiteboard/clear`, {
        method: 'POST',
        credentials: 'include',
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      setWhiteboardState(prev => (prev ? { ...prev, strokes: [], tokens: [] } : prev));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to clear whiteboard');
      throw err;
    } finally {
      setIsPending(false);
    }
  }, [sessionId, baseUrl]);

  // ── SSE event application ────────────────────────────────────────────────────

  const applySSEEvent = useCallback((event: WhiteboardSSEEvent) => {
    setWhiteboardState(prev => {
      if (!prev) return prev;
      switch (event.type) {
        case 'stroke-added':
          return event.stroke ? { ...prev, strokes: [...prev.strokes, event.stroke] } : prev;
        case 'structured-updated':
          return {
            ...prev,
            tokens: event.tokens ?? prev.tokens,
            gridSize: event.gridSize ?? prev.gridSize,
            showGrid: event.showGrid ?? prev.showGrid,
            mode: event.mode ?? prev.mode,
          };
        case 'whiteboard-cleared':
          return { ...prev, strokes: [], tokens: [] };
        default:
          return prev;
      }
    });
  }, []);

  // ── SSE listener — native live-session stream ─────────────────────────────────
  // Consumes `session:whiteboard` events from the native /api/v1/live-sessions stream.
  //
  // WHY re-fetch instead of delta-apply:
  // The backend emits per-event payloads with opaque fields (`dataJson` / `structuredJson`
  // as raw JSON strings) and per-stroke granularity, while the FE save-path is bulk
  // (POST /strokes with the full strokes array). There is no reliable field mapping between
  // the BE event shape and the FE WhiteboardSSEEvent shape — attempting delta-parse would
  // silently drop all events (the `type` discriminator field does NOT exist on BE payloads).
  // Re-fetching the authoritative GET /whiteboard state is correct for all four sub-types
  // (StrokeAdded / StrokeRemoved / StructuredUpdated / WhiteboardCleared) and is
  // shape-agnostic. Known MVP limitation: a remote event arriving during a local
  // unsaved-stroke window may momentarily reload server state, discarding in-flight
  // local strokes until the next local save completes.

  // Hold loadWhiteboard in a ref so the SSE effect doesn't need it in deps
  // (which would reopen the EventSource on every state change).
  const loadWhiteboardRef = useRef(loadWhiteboard);
  useEffect(() => {
    loadWhiteboardRef.current = loadWhiteboard;
  }, [loadWhiteboard]);

  useEffect(() => {
    const endpoint = `${baseUrl}/api/v1/live-sessions/${sessionId}/stream`;

    let source: EventSource | null = null;
    try {
      source = new EventSource(endpoint, { withCredentials: true });
    } catch {
      return;
    }

    // Debounce timer for coalescing rapid stroke-burst events into a single refetch.
    let refetchTimer: ReturnType<typeof setTimeout> | null = null;

    const handleWhiteboardEvent = (_event: MessageEvent<string>) => {
      // The SSE event is a pure signal — we do NOT parse the payload for delta fields.
      // Schedule a debounced re-fetch of the canonical whiteboard state.
      if (refetchTimer !== null) clearTimeout(refetchTimer);
      refetchTimer = setTimeout(() => {
        void loadWhiteboardRef.current();
      }, 200);
    };

    source.addEventListener('session:whiteboard', handleWhiteboardEvent);
    return () => {
      source?.close();
      if (refetchTimer !== null) clearTimeout(refetchTimer);
    };
  }, [sessionId, baseUrl]);

  // ── Load on mount ────────────────────────────────────────────────────────────

  useEffect(() => {
    void loadWhiteboard();
  }, [loadWhiteboard]);

  // ── Cleanup debounce timers on unmount ───────────────────────────────────────

  useEffect(() => {
    return () => {
      if (strokesTimerRef.current) clearTimeout(strokesTimerRef.current);
      if (structuredTimerRef.current) clearTimeout(structuredTimerRef.current);
    };
  }, []);

  return {
    whiteboardState,
    isLoading,
    isPending,
    error,
    saveStrokes,
    saveStructured,
    clear,
    applySSEEvent,
  };
}
