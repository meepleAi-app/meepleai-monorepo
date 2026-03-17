/**
 * useCounterTool Hook (Issue #4979)
 *
 * Manages counter tool state with optimistic UI and API sync.
 *
 * Features:
 * - Load counter state from server
 * - Optimistic updates: apply change immediately, revert on error
 * - Clamp values within [minValue, maxValue]
 * - Per-player and shared modes
 *
 * Backend endpoint:
 * - POST /api/v1/live-sessions/{sessionId}/tool-states/{toolName}/counter
 *   Body: { playerId: string, change: number }
 *   Response: ToolStateDto with stateData containing CounterState
 */

import { useState, useCallback } from 'react';

import type { CounterState } from '@/components/session/types';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface UseCounterToolOptions {
  sessionId: string;
  toolName: string;
  apiBaseUrl?: string;
}

export interface CounterToolState {
  /** Current counter state (null until loaded) */
  state: CounterState | null;
  /** Whether an update is in flight */
  isPending: boolean;
  /** Last error message */
  error: string | null;
  /** Increment or decrement the counter for a player */
  applyChange: (playerId: string, change: number) => Promise<void>;
  /** Set state directly (for SSE real-time updates) */
  applyState: (newState: CounterState) => void;
}

// ── Hook ──────────────────────────────────────────────────────────────────────

export function useCounterTool({
  sessionId,
  toolName,
  apiBaseUrl,
}: UseCounterToolOptions): CounterToolState {
  const [state, setState] = useState<CounterState | null>(null);
  const [isPending, setIsPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const baseUrl = apiBaseUrl ?? process.env.NEXT_PUBLIC_API_BASE ?? '';

  const applyChange = useCallback(
    async (playerId: string, change: number) => {
      if (!state) return;

      // Optimistic update
      const previous = state;
      setState(prev => {
        if (!prev) return prev;
        if (prev.isPerPlayer) {
          const currentVal = prev.playerValues[playerId] ?? prev.defaultValue;
          const newVal = Math.max(prev.minValue, Math.min(prev.maxValue, currentVal + change));
          return {
            ...prev,
            playerValues: { ...prev.playerValues, [playerId]: newVal },
          };
        } else {
          const newVal = Math.max(
            prev.minValue,
            Math.min(prev.maxValue, prev.currentValue + change)
          );
          return { ...prev, currentValue: newVal };
        }
      });

      setIsPending(true);
      setError(null);

      try {
        const res = await fetch(
          `${baseUrl}/api/v1/live-sessions/${sessionId}/tool-states/${toolName}/counter`,
          {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ playerId, change }),
          }
        );

        if (!res.ok) {
          throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        }

        // Reconcile with server response
        const dto = await res.json();
        if (dto?.stateData) {
          setState(dto.stateData as CounterState);
        }
      } catch (err) {
        // Revert on error
        setState(previous);
        setError(err instanceof Error ? err.message : 'Failed to update counter');
      } finally {
        setIsPending(false);
      }
    },
    [state, sessionId, toolName, baseUrl]
  );

  const applyState = useCallback((newState: CounterState) => {
    setState(newState);
  }, []);

  return { state, isPending, error, applyChange, applyState };
}
