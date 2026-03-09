'use client';

import { useState, useEffect } from 'react';

import { Activity, RefreshCw } from 'lucide-react';

import { AdminHubEmptyState } from '@/components/admin/layout/AdminHubEmptyState';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

interface AiRequestRow {
  id: string;
  model: string | null;
  endpoint: string;
  tokenCount: number;
  latencyMs: number;
  status: string;
  createdAt: string;
}

export function RequestsTab() {
  const [requests, setRequests] = useState<AiRequestRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);

  const fetchRequests = (pageNum: number) => {
    setLoading(true);
    api.admin
      .getAiRequests({ page: pageNum, pageSize: 20 })
      .then(data => {
        const items = (data as Record<string, unknown>)?.requests;
        setRequests(Array.isArray(items) ? (items as AiRequestRow[]) : []);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchRequests(page);
  }, [page]);

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            AI Requests
          </h2>
          <p className="text-sm text-muted-foreground mt-0.5">
            Recent LLM API calls with latency, tokens, and cost breakdown.
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

      {loading ? (
        <div className="space-y-2">
          {[1, 2, 3, 4, 5].map(i => (
            <div
              key={i}
              className="h-14 rounded-xl bg-white/40 dark:bg-zinc-800/40 animate-pulse"
            />
          ))}
        </div>
      ) : requests.length > 0 ? (
        <>
          {/* Mobile: card layout */}
          <div className="space-y-2 md:hidden">
            {requests.map((r, idx) => (
              <div
                key={r.id ?? idx}
                className="rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-3"
              >
                <div className="flex items-center justify-between gap-2">
                  <span className="font-mono text-xs text-foreground truncate max-w-[60%]">
                    {r.model ?? '—'}
                  </span>
                  <StatusBadge status={r.status} />
                </div>
                <p className="mt-1 text-xs text-muted-foreground truncate">{r.endpoint ?? '—'}</p>
                <div className="mt-2 flex items-center gap-4 text-xs text-muted-foreground">
                  <span className="tabular-nums">
                    {r.tokenCount?.toLocaleString() ?? '—'} tokens
                  </span>
                  <span className="tabular-nums">{r.latencyMs ? `${r.latencyMs}ms` : '—'}</span>
                </div>
              </div>
            ))}
          </div>

          {/* Desktop: table layout */}
          <div className="hidden md:block rounded-xl border border-slate-200/60 dark:border-zinc-700/40 overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200/60 dark:border-zinc-700/40 bg-muted/30">
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
                {requests.map((r, idx) => (
                  <tr
                    key={r.id ?? idx}
                    className="border-b border-slate-100/60 dark:border-zinc-800/40 last:border-0"
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
  );
}

function StatusBadge({ status }: { status: string }) {
  return (
    <span
      className={cn(
        'inline-block rounded-full px-2 py-0.5 text-[10px] font-medium',
        status === 'Success'
          ? 'bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400'
          : 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'
      )}
    >
      {status}
    </span>
  );
}
