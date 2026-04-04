/**
 * useGameToolkit — Fetches the published GameToolkit for a game (Issue #4976).
 *
 * Calls GET /api/v1/game-toolkits/by-game/{gameId} and returns the first
 * published toolkit found (toolkits are versioned; the most recent published
 * one drives override logic for the session page).
 *
 * Returns null toolkit when:
 *  - gameId is falsy (Generic session with no linked game)
 *  - No published toolkit exists for the game
 *  - Network error (error is surfaced separately)
 */

import { useState, useEffect } from 'react';

import type { GameToolkitDto } from '../types/gameToolkit';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface UseGameToolkitResult {
  /** The published toolkit for this game, or null if none. */
  toolkit: GameToolkitDto | null;
  isLoading: boolean;
  error: string | null;
}

// ── Hook ──────────────────────────────────────────────────────────────────────

/**
 * @param gameId - The shared-catalog game ID, or null/undefined for Generic sessions.
 */
export function useGameToolkit(gameId: string | null | undefined): UseGameToolkitResult {
  const [toolkit, setToolkit] = useState<GameToolkitDto | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!gameId) {
      setToolkit(null);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setError(null);

    const baseUrl = process.env.NEXT_PUBLIC_API_BASE ?? '';

    fetch(`${baseUrl}/api/v1/game-toolkits/by-game/${gameId}`, {
      method: 'GET',
      credentials: 'include',
    })
      .then(async res => {
        if (!res.ok) {
          // 404 = no toolkit configured for this game (not an error)
          if (res.status === 404) return [];
          throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        }
        return res.json() as Promise<GameToolkitDto[]>;
      })
      .then((toolkits: GameToolkitDto[]) => {
        if (cancelled) return;
        // Use the first published toolkit (sorted by version desc server-side)
        const published = toolkits.find(t => t.isPublished) ?? null;
        setToolkit(published);
        setIsLoading(false);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : 'Failed to load toolkit');
        setToolkit(null);
        setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [gameId]);

  return { toolkit, isLoading, error };
}
