'use client';

import { useState, useEffect, useMemo } from 'react';

import { Activity, RefreshCw } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { AiTrendChart, type TrendDatapoint } from '@/components/admin/ai/AiTrendChart';
import { QueryDrillPanel } from '@/components/admin/ai/QueryDrillPanel';
import { AdminHubEmptyState } from '@/components/admin/layout/AdminHubEmptyState';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { AiRequest } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

const TREND_RANGE_OPTIONS = ['1d', '7d', '30d'] as const;
const TREND_RANGE_DAYS: Record<string, number> = { '1d': 1, '7d': 7, '30d': 30 };

export function RequestsTab() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const selectedId = searchParams?.get('queryId') ?? null;
  const range = searchParams?.get('range') ?? '7d';

  const [requests, setRequests] = useState<AiRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [trend, setTrend] = useState<TrendDatapoint[]>([]);

  const fetchRequests = (pageNum: number) => {
    setLoading(true);
    api.admin
      .getAiRequests({ page: pageNum, pageSize: 20 })
      .then(data => {
        setRequests(Array.isArray(data?.requests) ? data.requests : []);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchRequests(page);
  }, [page]);

  useEffect(() => {
    const days = TREND_RANGE_DAYS[range] ?? 7;
    let cancelled = false;
    api.admin
      .getModelPerformance(days)
      .then(data => {
        if (cancelled) return;
        const points = (data?.dailyStats ?? []).map(d => ({
          date: d.date,
          avgLatencyMs: d.avgLatencyMs,
          requestCount: d.requestCount,
        }));
        setTrend(points);
      })
      .catch(() => {
        if (!cancelled) setTrend([]);
      });
    return () => {
      cancelled = true;
    };
  }, [range]);

  const setRange = (next: string) => {
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.set('range', next);
    if (!params.get('tab')) params.set('tab', 'requests');
    router.replace(`?${params.toString()}`, { scroll: false });
  };

  const selectedQuery = useMemo<AiRequest | null>(() => {
    if (!selectedId) return null;
    return requests.find(r => r.id === selectedId) ?? null;
  }, [requests, selectedId]);

  const openDrill = (id: string) => {
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.set('queryId', id);
    if (!params.get('tab')) params.set('tab', 'requests');
    router.replace(`?${params.toString()}`, { scroll: false });
  };

  const closeDrill = () => {
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.delete('queryId');
    router.replace(`?${params.toString()}`, { scroll: false });
  };

  const hasDrill = selectedQuery !== null;

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            AI Requests
          </h2>
          <p className="text-sm text-muted-foreground mt-0.5">
            Recent LLM API calls with latency, tokens, and cost breakdown. Click a row to inspect.
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => fetchRequests(page)}
          className="self-start sm:self-auto"
        >
          <RefreshCw className="mr-1.5 h-3.5 w-3.5" /> Refresh
        </Button>
      </div>

      <AiTrendChart
        data={trend}
        range={range}
        rangeOptions={TREND_RANGE_OPTIONS}
        onRangeChange={setRange}
      />

      <div
        className={cn(
          'grid gap-4',
          hasDrill ? 'lg:grid-cols-[minmax(0,58fr)_minmax(0,42fr)]' : 'grid-cols-1'
        )}
      >
        <div className="space-y-2">
          {loading ? (
            <div className="space-y-2">
              {[1, 2, 3, 4, 5].map(i => (
                <div key={i} className="h-14 rounded-xl bg-card/40 animate-pulse" />
              ))}
            </div>
          ) : requests.length > 0 ? (
            <>
              {/* Mobile: card layout */}
              <div className="space-y-2 md:hidden">
                {requests.map(r => (
                  <button
                    type="button"
                    key={r.id}
                    onClick={() => openDrill(r.id)}
                    aria-pressed={r.id === selectedId}
                    className={cn(
                      'w-full text-left rounded-xl border bg-card/70 backdrop-blur-md p-3 transition-colors',
                      r.id === selectedId
                        ? 'border-primary/50 bg-primary/5'
                        : 'border-border/60 hover:border-primary/30'
                    )}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <span className="font-mono text-xs text-foreground truncate max-w-[60%]">
                        {r.model ?? '—'}
                      </span>
                      <StatusBadge status={r.status} />
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground truncate">
                      {r.endpoint ?? '—'}
                    </p>
                    <div className="mt-2 flex items-center gap-4 text-xs text-muted-foreground">
                      <span className="tabular-nums">
                        {r.tokenCount?.toLocaleString() ?? '—'} tokens
                      </span>
                      <span className="tabular-nums">{r.latencyMs ? `${r.latencyMs}ms` : '—'}</span>
                    </div>
                  </button>
                ))}
              </div>

              {/* Desktop: table layout */}
              <div className="hidden md:block rounded-xl border border-border/60 overflow-hidden">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-border/60 bg-muted/30">
                      <th className="text-left px-4 py-2.5 text-xs font-medium text-muted-foreground">
                        Model
                      </th>
                      <th className="text-left px-4 py-2.5 text-xs font-medium text-muted-foreground">
                        Endpoint
                      </th>
                      <th className="text-right px-4 py-2.5 text-xs font-medium text-muted-foreground">
                        Tokens
                      </th>
                      <th className="text-right px-4 py-2.5 text-xs font-medium text-muted-foreground">
                        Latency
                      </th>
                      <th className="text-right px-4 py-2.5 text-xs font-medium text-muted-foreground">
                        Status
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {requests.map(r => (
                      <tr
                        key={r.id}
                        onClick={() => openDrill(r.id)}
                        aria-pressed={r.id === selectedId}
                        className={cn(
                          'cursor-pointer border-b border-border/60 last:border-0 transition-colors',
                          r.id === selectedId ? 'bg-primary/5' : 'hover:bg-muted/40'
                        )}
                      >
                        <td className="px-4 py-2.5 font-mono text-xs text-foreground">
                          {r.model ?? '—'}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-muted-foreground truncate max-w-[200px]">
                          {r.endpoint ?? '—'}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-right text-foreground tabular-nums">
                          {r.tokenCount?.toLocaleString() ?? '—'}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-right text-muted-foreground tabular-nums">
                          {r.latencyMs ? `${r.latencyMs}ms` : '—'}
                        </td>
                        <td className="px-4 py-2.5 text-xs text-right">
                          <StatusBadge status={r.status} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Pagination */}
              <div className="flex items-center justify-between">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => setPage(p => p - 1)}
                >
                  Previous
                </Button>
                <span className="text-xs text-muted-foreground tabular-nums">Page {page}</span>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={requests.length < 20}
                  onClick={() => setPage(p => p + 1)}
                >
                  Next
                </Button>
              </div>
            </>
          ) : (
            <AdminHubEmptyState
              icon={<Activity />}
              title="No AI requests found"
              description="AI request logs will appear here once agents start processing queries."
            />
          )}
        </div>

        {hasDrill && (
          <div className="hidden lg:block">
            <QueryDrillPanel query={selectedQuery} onClose={closeDrill} />
          </div>
        )}
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  return (
    <span
      className={cn(
        'inline-block rounded-full px-2 py-0.5 text-[10px] font-medium',
        status === 'Success' ? 'bg-success/15 text-success' : 'bg-destructive/15 text-destructive'
      )}
    >
      {status}
    </span>
  );
}
