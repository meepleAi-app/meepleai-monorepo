import React from 'react';

import type { SessionDetailData } from '../types';

// ============================================================================
// Data Fetching Hook — Session
// ============================================================================

interface UseSessionDetailResult {
  data: SessionDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

export function useSessionDetail(sessionId: string): UseSessionDetailResult {
  const [data, setData] = React.useState<SessionDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fetchData = React.useCallback(
    async (signal: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(`/api/v1/sessions/${sessionId}`, { signal });
        if (!res.ok) throw new Error(`Errore ${res.status}: sessione non trovata`);
        const json = (await res.json()) as Record<string, unknown>;
        setData(mapToSessionDetailData(json));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(err instanceof Error ? err.message : 'Impossibile caricare i dati della sessione');
      } finally {
        setLoading(false);
      }
    },
    [sessionId]
  );

  React.useEffect(() => {
    if (!sessionId) return;
    const controller = new AbortController();
    void fetchData(controller.signal);
    return () => {
      controller.abort();
    };
  }, [sessionId, fetchData]);

  const retry = React.useCallback(() => {
    const controller = new AbortController();
    void fetchData(controller.signal);
  }, [fetchData]);

  return { data, loading, error, retry };
}

function mapToSessionDetailData(json: Record<string, unknown>): SessionDetailData {
  const game = json.game as Record<string, unknown> | undefined;

  return {
    id: String(json.id ?? ''),
    sessionCode: String(json.sessionCode ?? ''),
    status: (json.status as SessionDetailData['status']) ?? 'Setup',
    title: String(json.title ?? json.gameName ?? 'Sessione'),
    gameId: json.gameId != null ? String(json.gameId) : undefined,
    gameName: json.gameName != null ? String(json.gameName) : undefined,
    gameImageUrl:
      json.gameImageUrl != null
        ? String(json.gameImageUrl)
        : game?.imageUrl != null
          ? String(game.imageUrl)
          : undefined,
    startedAt: json.startedAt != null ? String(json.startedAt) : undefined,
    completedAt: json.completedAt != null ? String(json.completedAt) : undefined,
    players: Array.isArray(json.players) ? (json.players as SessionDetailData['players']) : [],
    toolkit: json.toolkit != null ? (json.toolkit as SessionDetailData['toolkit']) : undefined,
    timeline: Array.isArray(json.timeline)
      ? (json.timeline as SessionDetailData['timeline'])
      : Array.isArray(json.events)
        ? (json.events as SessionDetailData['timeline'])
        : [],
  };
}
