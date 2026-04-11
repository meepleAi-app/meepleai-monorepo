import React from 'react';

import type { ToolDetailData } from '../types';

// ============================================================================
// Data Fetching Hook — Tool
// ============================================================================

interface UseToolDetailResult {
  data: ToolDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

export function useToolDetail(toolId: string): UseToolDetailResult {
  const [data, setData] = React.useState<ToolDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fetchData = React.useCallback(
    async (signal: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(`/api/v1/tools/${toolId}`, { signal });
        if (!res.ok) throw new Error(`Errore ${res.status}: strumento non trovato`);
        const json = (await res.json()) as Record<string, unknown>;
        setData(mapToToolDetailData(json));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(
          err instanceof Error ? err.message : 'Impossibile caricare i dati dello strumento'
        );
      } finally {
        setLoading(false);
      }
    },
    [toolId]
  );

  React.useEffect(() => {
    if (!toolId) return;
    const controller = new AbortController();
    void fetchData(controller.signal);
    return () => {
      controller.abort();
    };
  }, [toolId, fetchData]);

  const retry = React.useCallback(() => {
    const controller = new AbortController();
    void fetchData(controller.signal);
  }, [fetchData]);

  return { data, loading, error, retry };
}

function mapToToolDetailData(json: Record<string, unknown>): ToolDetailData {
  const rawType = String(json.toolType ?? 'dice');
  const toolType: ToolDetailData['toolType'] =
    rawType === 'dice' || rawType === 'card' || rawType === 'timer' || rawType === 'counter'
      ? rawType
      : 'dice';

  return {
    id: String(json.id ?? ''),
    name: String(json.name ?? ''),
    toolType,
    toolkitId: json.toolkitId != null ? String(json.toolkitId) : undefined,
    toolkitName: json.toolkitName != null ? String(json.toolkitName) : undefined,
    isOwner: Boolean(json.isOwner ?? false),
    hasActiveSession: Boolean(json.hasActiveSession ?? false),
    config:
      json.config != null && typeof json.config === 'object' && !Array.isArray(json.config)
        ? (json.config as Record<string, string | number | boolean>)
        : {},
    previewDescription:
      json.previewDescription != null ? String(json.previewDescription) : undefined,
  };
}
