'use client';
/* eslint-disable @typescript-eslint/no-non-null-assertion -- pre-existing pattern: array/object access guarded by length/key check or by upstream validator; assertion is correct by construction. Cleanup tracked for follow-up audit. */

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CpuIcon, Loader2Icon, RefreshCwIcon } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { apiClient } from '@/lib/api/client';

// ── Types ─────────────────────────────────────────────────────────────

interface EmbeddingInfo {
  model: string;
  device: string;
  dimensions: number;
  maxTokens?: number;
}

interface EmbeddingMetrics {
  totalRequests: number;
  totalFailures: number;
  failureRate: number;
  avgDurationMs: number;
}

interface VectorGameStats {
  gameId: string;
  gameName: string;
  vectorCount: number;
  documentCount: number;
}

interface VectorStatsResponse {
  games: VectorGameStats[];
  totalVectors: number;
  totalDocuments: number;
}

// ── API ───────────────────────────────────────────────────────────────

async function fetchEmbeddingInfo(): Promise<EmbeddingInfo> {
  const result = await apiClient.get<EmbeddingInfo>('/api/v1/admin/embedding/info');
  return result ?? { model: 'unknown', device: 'unknown', dimensions: 0 };
}

async function fetchEmbeddingMetrics(): Promise<EmbeddingMetrics> {
  const result = await apiClient.get<EmbeddingMetrics>('/api/v1/admin/embedding/metrics');
  return result ?? { totalRequests: 0, totalFailures: 0, failureRate: 0, avgDurationMs: 0 };
}

async function fetchVectorStats(): Promise<VectorStatsResponse> {
  const result = await apiClient.get<VectorStatsResponse>('/api/v1/admin/kb/vector-stats');
  return result ?? { games: [], totalVectors: 0, totalDocuments: 0 };
}

// ── Main Component ────────────────────────────────────────────────────

export function EmbeddingTab() {
  const queryClient = useQueryClient();

  const { data: info, isLoading: infoLoading } = useQuery({
    queryKey: ['admin', 'embedding', 'info'],
    queryFn: fetchEmbeddingInfo,
    staleTime: 30_000,
    refetchInterval: 30_000,
  });

  const { data: metrics, isLoading: metricsLoading } = useQuery({
    queryKey: ['admin', 'embedding', 'metrics'],
    queryFn: fetchEmbeddingMetrics,
    staleTime: 30_000,
    refetchInterval: 30_000,
  });

  const { data: vectorStats, isLoading: vectorLoading } = useQuery({
    queryKey: ['admin', 'kb', 'vector-stats'],
    queryFn: fetchVectorStats,
    staleTime: 30_000,
    refetchInterval: 30_000,
  });

  const isLoading = infoLoading || metricsLoading || vectorLoading;

  function handleRefresh() {
    queryClient.invalidateQueries({ queryKey: ['admin', 'embedding'] });
    queryClient.invalidateQueries({ queryKey: ['admin', 'kb', 'vector-stats'] });
  }

  return (
    <div className="space-y-4">
      {/* Header with refresh */}
      <div className="flex items-center justify-between">
        <h2 className="text-base font-semibold flex items-center gap-2">
          <CpuIcon className="h-4 w-4" />
          Embedding Service
        </h2>
        <button
          type="button"
          onClick={handleRefresh}
          className="flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground transition-colors px-2 py-1 rounded hover:bg-muted"
        >
          <RefreshCwIcon className="h-3.5 w-3.5" />
          Refresh
        </button>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2Icon className="h-5 w-5 animate-spin text-muted-foreground" />
        </div>
      ) : (
        <>
          {/* Service Info */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Service Info</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
                <InfoCell label="Model" value={info?.model ?? '\u2014'} />
                <InfoCell label="Device" value={info?.device ?? '\u2014'} />
                <InfoCell label="Dimensions" value={info?.dimensions?.toString() ?? '\u2014'} />
                {info?.maxTokens != null && (
                  <InfoCell label="Max Tokens" value={info.maxTokens.toString()} />
                )}
              </div>
            </CardContent>
          </Card>

          {/* Metrics */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">Metrics</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
                <MetricCell label="Total Requests" value={metrics?.totalRequests ?? 0} />
                <MetricCell label="Failures" value={metrics?.totalFailures ?? 0} />
                <MetricCell
                  label="Failure Rate"
                  value={`${((metrics?.failureRate ?? 0) * 100).toFixed(1)}%`}
                />
                <MetricCell
                  label="Avg Duration"
                  value={`${Math.round(metrics?.avgDurationMs ?? 0)}ms`}
                />
              </div>
            </CardContent>
          </Card>

          {/* Vector Stats */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">
                Vector Stats ({vectorStats?.totalVectors ?? 0} vectors,{' '}
                {vectorStats?.totalDocuments ?? 0} docs)
              </CardTitle>
            </CardHeader>
            <CardContent>
              {(vectorStats?.games ?? []).length === 0 ? (
                <p className="text-sm text-muted-foreground text-center py-6">
                  No vector data available
                </p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b text-left text-muted-foreground">
                        <th className="pb-2 font-medium">Game</th>
                        <th className="pb-2 font-medium text-right">Vectors</th>
                        <th className="pb-2 font-medium text-right">Documents</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y">
                      {vectorStats!.games.map(game => (
                        <tr key={game.gameId} className="hover:bg-muted/50">
                          <td className="py-2 pr-4 truncate max-w-[280px]" title={game.gameName}>
                            {game.gameName}
                          </td>
                          <td className="py-2 text-right tabular-nums">{game.vectorCount}</td>
                          <td className="py-2 text-right tabular-nums">{game.documentCount}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────

function InfoCell({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-sm font-medium truncate" title={value}>
        {value}
      </p>
    </div>
  );
}

function MetricCell({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-lg border bg-white/50 dark:bg-zinc-800/50 p-3 text-center">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-xl font-bold tabular-nums">{value}</p>
    </div>
  );
}
