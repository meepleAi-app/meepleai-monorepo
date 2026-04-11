import React from 'react';

import type { ToolkitDetailData } from '../types';

// ============================================================================
// Data Fetching Hook — Toolkit
// ============================================================================

interface UseToolkitDetailResult {
  data: ToolkitDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

export function useToolkitDetail(toolkitId: string): UseToolkitDetailResult {
  const [data, setData] = React.useState<ToolkitDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fetchData = React.useCallback(
    async (signal: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(`/api/v1/toolkits/${toolkitId}`, { signal });
        if (!res.ok) throw new Error(`Errore ${res.status}: toolkit non trovato`);
        const json = (await res.json()) as Record<string, unknown>;
        setData(mapToToolkitDetailData(json));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(err instanceof Error ? err.message : 'Impossibile caricare i dati del toolkit');
      } finally {
        setLoading(false);
      }
    },
    [toolkitId]
  );

  React.useEffect(() => {
    if (!toolkitId) return;
    const controller = new AbortController();
    void fetchData(controller.signal);
    return () => {
      controller.abort();
    };
  }, [toolkitId, fetchData]);

  const retry = React.useCallback(() => {
    const controller = new AbortController();
    void fetchData(controller.signal);
  }, [fetchData]);

  return { data, loading, error, retry };
}

function mapToToolkitDetailData(json: Record<string, unknown>): ToolkitDetailData {
  return {
    id: String(json.id ?? ''),
    name: String(json.name ?? ''),
    description: json.description != null ? String(json.description) : undefined,
    version: typeof json.version === 'number' ? json.version : 1,
    isPublished: Boolean(json.isPublished ?? false),
    isOwner: Boolean(json.isOwner ?? false),
    gameId: json.gameId != null ? String(json.gameId) : undefined,
    gameName: json.gameName != null ? String(json.gameName) : undefined,
    diceTools: Array.isArray(json.diceTools)
      ? (json.diceTools as ToolkitDetailData['diceTools'])
      : [],
    cardTools: Array.isArray(json.cardTools)
      ? (json.cardTools as ToolkitDetailData['cardTools'])
      : [],
    timerTools: Array.isArray(json.timerTools)
      ? (json.timerTools as ToolkitDetailData['timerTools'])
      : [],
    counterTools: Array.isArray(json.counterTools)
      ? (json.counterTools as ToolkitDetailData['counterTools'])
      : [],
    history: Array.isArray(json.history) ? (json.history as ToolkitDetailData['history']) : [],
  };
}
