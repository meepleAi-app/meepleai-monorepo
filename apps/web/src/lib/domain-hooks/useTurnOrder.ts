/**
 * useTurnOrder Hook (Issue #4975)
 *
 * Manages turn order state, API calls, and real-time SSE updates.
 *
 * Features:
 * - Load, initialize, advance, and reset turn order via REST API
 * - Real-time turn advance updates via v2 SSE stream (session:toolkit events)
 * - Exposes applySSEAdvance for manual SSE integration
 *
 * Backend endpoints:
 * - GET    /api/v1/live-sessions/{sessionId}/turn-order
 * - POST   /api/v1/live-sessions/{sessionId}/turn-order/initialize
 * - POST   /api/v1/live-sessions/{sessionId}/turn-order/advance
 * - POST   /api/v1/live-sessions/{sessionId}/turn-order/reset
 *
 * @example
 * ```typescript
 * const { turnOrder, advance, initialize, isAdvancing } = useTurnOrder({ sessionId });
 * await advance();
 * ```
 */

import { useState, useCallback, useEffect, useRef } from 'react';

import type { TurnOrderData, TurnAdvancedPayload } from '@/components/session/types';

/**
 * Hook options
 */
export interface UseTurnOrderOptions {
  /** Session ID */
  sessionId: string;
  /** Optional API base URL */
  apiBaseUrl?: string;
  /** Callback fired when a turn:advanced SSE event is received */
  onTurnAdvanced?: (payload: TurnAdvancedPayload) => void;
}

/**
 * Hook return value
 */
export interface TurnOrderHookState {
  /** Current turn order state (null when not yet initialized) */
  turnOrder: TurnOrderData | null;
  /** True while loading the turn order or during initialize/reset */
  isLoading: boolean;
  /** True while advancing the turn */
  isAdvancing: boolean;
  /** Error message if last operation failed */
  error: string | null;
  /** Initialize turn order with the given player names */
  initialize: (playerOrder: string[]) => Promise<TurnOrderData>;
  /** Advance to the next player */
  advance: () => Promise<TurnOrderData>;
  /** Reset to round 1, first player */
  reset: () => Promise<TurnOrderData>;
  /** Reload turn order from server */
  loadTurnOrder: () => Promise<void>;
  /** Manually apply a SSE turn-advanced payload (for external SSE integration) */
  applySSEAdvance: (payload: TurnAdvancedPayload) => void;
}

/**
 * useTurnOrder Hook
 *
 * Manages turn order operations, state, and real-time updates.
 */
export function useTurnOrder({
  sessionId,
  apiBaseUrl,
  onTurnAdvanced,
}: UseTurnOrderOptions): TurnOrderHookState {
  const [turnOrder, setTurnOrder] = useState<TurnOrderData | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isAdvancing, setIsAdvancing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const baseUrl = apiBaseUrl ?? process.env.NEXT_PUBLIC_API_BASE ?? '';

  // Keep onTurnAdvanced in a ref to avoid re-creating SSE listener on every render
  const onTurnAdvancedRef = useRef(onTurnAdvanced);
  useEffect(() => {
    onTurnAdvancedRef.current = onTurnAdvanced;
  }, [onTurnAdvanced]);

  // ── REST Operations ──────────────────────────────────────────────────────────

  const loadTurnOrder = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await fetch(`${baseUrl}/api/v1/live-sessions/${sessionId}/turn-order`, {
        credentials: 'include',
      });
      if (res.status === 404) {
        setTurnOrder(null);
        return;
      }
      if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      const data: TurnOrderData = await res.json();
      setTurnOrder(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load turn order');
    } finally {
      setIsLoading(false);
    }
  }, [sessionId, baseUrl]);

  const initialize = useCallback(
    async (playerOrder: string[]): Promise<TurnOrderData> => {
      setIsLoading(true);
      setError(null);
      try {
        const res = await fetch(
          `${baseUrl}/api/v1/live-sessions/${sessionId}/turn-order/initialize`,
          {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ playerOrder }),
          }
        );
        if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        const data: TurnOrderData = await res.json();
        setTurnOrder(data);
        return data;
      } catch (err) {
        const msg = err instanceof Error ? err.message : 'Failed to initialize turn order';
        setError(msg);
        throw err;
      } finally {
        setIsLoading(false);
      }
    },
    [sessionId, baseUrl]
  );

  const advance = useCallback(async (): Promise<TurnOrderData> => {
    setIsAdvancing(true);
    setError(null);
    try {
      const res = await fetch(`${baseUrl}/api/v1/live-sessions/${sessionId}/turn-order/advance`, {
        method: 'POST',
        credentials: 'include',
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      const data: TurnOrderData = await res.json();
      setTurnOrder(data);
      return data;
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to advance turn';
      setError(msg);
      throw err;
    } finally {
      setIsAdvancing(false);
    }
  }, [sessionId, baseUrl]);

  const reset = useCallback(async (): Promise<TurnOrderData> => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await fetch(`${baseUrl}/api/v1/live-sessions/${sessionId}/turn-order/reset`, {
        method: 'POST',
        credentials: 'include',
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      const data: TurnOrderData = await res.json();
      setTurnOrder(data);
      return data;
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to reset turn order';
      setError(msg);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [sessionId, baseUrl]);

  // ── Manual SSE application (for external SSE integration) ───────────────────

  const applySSEAdvance = useCallback((payload: TurnAdvancedPayload) => {
    setTurnOrder(prev => {
      if (!prev) return prev;
      const newIndex = prev.playerOrder.findIndex(n => n === payload.currentPlayerName);
      return {
        ...prev,
        currentIndex: newIndex >= 0 ? newIndex : prev.currentIndex,
        currentPlayer: payload.currentPlayerName,
        nextPlayer: payload.nextPlayerName,
        roundNumber: payload.roundNumber,
      };
    });
    onTurnAdvancedRef.current?.(payload);
  }, []);

  // ── SSE v2 for real-time turn advances from other participants ────────────────
  useEffect(() => {
    const endpoint = `${baseUrl}/api/v1/game-sessions/${sessionId}/stream/v2`;

    let source: EventSource | null = null;
    try {
      source = new EventSource(endpoint, { withCredentials: true });
    } catch {
      // EventSource not available (e.g., server-side rendering)
      return;
    }

    const handleToolkitEvent = (event: MessageEvent<string>) => {
      try {
        const payload = JSON.parse(event.data) as Record<string, unknown>;
        // Discriminate TurnAdvancedEvent by its unique fields
        if (
          typeof payload['currentPlayerName'] === 'string' &&
          typeof payload['roundNumber'] === 'number' &&
          typeof payload['previousPlayerName'] === 'string'
        ) {
          applySSEAdvance({
            currentPlayerName: payload['currentPlayerName'],
            previousPlayerName: payload['previousPlayerName'],
            nextPlayerName:
              typeof payload['nextPlayerName'] === 'string' ? payload['nextPlayerName'] : '',
            roundNumber: payload['roundNumber'],
          });
        }
      } catch {
        // Silently ignore malformed events
      }
    };

    source.addEventListener('session:toolkit', handleToolkitEvent);

    return () => {
      source?.close();
    };
  }, [sessionId, baseUrl, applySSEAdvance]);

  // Load turn order on mount
  useEffect(() => {
    void loadTurnOrder();
  }, [loadTurnOrder]);

  return {
    turnOrder,
    isLoading,
    isAdvancing,
    error,
    initialize,
    advance,
    reset,
    loadTurnOrder,
    applySSEAdvance,
  };
}
