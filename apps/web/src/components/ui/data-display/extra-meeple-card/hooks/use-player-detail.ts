import React from 'react';

import type { PlayerDetailData } from '../types';

// ============================================================================
// Data Fetching Hook — Player
// ============================================================================

interface UsePlayerDetailResult {
  data: PlayerDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

export function usePlayerDetail(playerId: string): UsePlayerDetailResult {
  const [data, setData] = React.useState<PlayerDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fetchData = React.useCallback(
    async (signal: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(`/api/v1/players/${playerId}`, { signal });
        if (!res.ok) throw new Error(`Errore ${res.status}: giocatore non trovato`);
        const json = (await res.json()) as Record<string, unknown>;
        setData(mapToPlayerDetailData(json));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(err instanceof Error ? err.message : 'Impossibile caricare i dati del giocatore');
      } finally {
        setLoading(false);
      }
    },
    [playerId]
  );

  React.useEffect(() => {
    if (!playerId) return;
    const controller = new AbortController();
    void fetchData(controller.signal);
    return () => {
      controller.abort();
    };
  }, [playerId, fetchData]);

  const retry = React.useCallback(() => {
    const controller = new AbortController();
    void fetchData(controller.signal);
  }, [fetchData]);

  return { data, loading, error, retry };
}

function mapToPlayerDetailData(json: Record<string, unknown>): PlayerDetailData {
  return {
    id: String(json.id ?? ''),
    displayName: String(json.displayName ?? json.userName ?? ''),
    avatarUrl: json.avatarUrl != null ? String(json.avatarUrl) : undefined,
    gamesPlayed: Number(json.gamesPlayed ?? 0),
    winRate: Number(json.winRate ?? 0),
    totalSessions: Number(json.totalSessions ?? 0),
    favoriteGame: json.favoriteGame != null ? String(json.favoriteGame) : undefined,
    achievements: Array.isArray(json.achievements)
      ? (json.achievements as { id: string; name: string; icon: string }[])
      : [],
    recentGames: Array.isArray(json.recentGames)
      ? (json.recentGames as { name: string; date: string; result: 'win' | 'loss' | 'draw' }[])
      : [],
  };
}
