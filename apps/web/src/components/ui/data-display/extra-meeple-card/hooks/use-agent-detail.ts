import React from 'react';

import type { AgentDetailData } from '../types';

// ============================================================================
// Data Fetching Hook — Agent
// ============================================================================

interface UseAgentDetailResult {
  data: AgentDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

export function useAgentDetail(agentId: string): UseAgentDetailResult {
  const [data, setData] = React.useState<AgentDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fetchData = React.useCallback(
    async (signal: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(`/api/v1/agents/${agentId}`, { signal });
        if (!res.ok) {
          throw new Error(`Errore ${res.status}: agente non trovato`);
        }
        const json = (await res.json()) as Record<string, unknown>;
        setData(mapToAgentDetailData(json));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(err instanceof Error ? err.message : "Impossibile caricare i dati dell'agente");
      } finally {
        setLoading(false);
      }
    },
    [agentId]
  );

  React.useEffect(() => {
    if (!agentId) return;
    const controller = new AbortController();
    void fetchData(controller.signal);
    return () => {
      controller.abort();
    };
  }, [agentId, fetchData]);

  const retry = React.useCallback(() => {
    const controller = new AbortController();
    void fetchData(controller.signal);
  }, [fetchData]);

  return { data, loading, error, retry };
}

function mapToAgentDetailData(json: Record<string, unknown>): AgentDetailData {
  const params =
    json.strategyParameters != null && typeof json.strategyParameters === 'object'
      ? (json.strategyParameters as Record<string, unknown>)
      : {};
  return {
    id: String(json.id ?? ''),
    name: String(json.name ?? ''),
    type: String(json.type ?? ''),
    strategyName: String(json.strategyName ?? ''),
    strategyParameters: params,
    isActive: Boolean(json.isActive ?? false),
    isIdle: Boolean(json.isIdle ?? true),
    invocationCount: json.invocationCount != null ? Number(json.invocationCount) : 0,
    lastInvokedAt: json.lastInvokedAt != null ? String(json.lastInvokedAt) : null,
    createdAt: String(json.createdAt ?? new Date().toISOString()),
    gameId: json.gameId != null ? String(json.gameId) : undefined,
    gameName: json.gameName != null ? String(json.gameName) : undefined,
  };
}
