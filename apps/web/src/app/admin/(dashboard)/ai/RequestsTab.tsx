'use client';

import { useState, useEffect } from 'react';

import { Activity, RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

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
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            AI Requests
          </h2>
          <p className="text-sm text-muted-foreground mt-1">
            Recent LLM API calls with latency, tokens, and cost breakdown.
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => fetchRequests(page)}>
          <RefreshCw className="mr-1.5 h-3.5 w-3.5" /> Refresh
        </Button>
      </div>

      {loading ? (
        <div className="space-y-2">
          {[1, 2, 3, 4, 5].map(i => (
            <div
              key={i}
              className="h-14 rounded-lg bg-white/40 dark:bg-zinc-800/40 animate-pulse"
            />
          ))}
        </div>
      ) : requests.length > 0 ? (
        <>
          <div className="rounded-xl border border-slate-200/60 dark:border-zinc-700/40 overflow-hidden">
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
                    <td className="px-4 py-2.5 text-xs text-right text-foreground">
                      <span
                        className={`inline-block rounded-full px-2 py-0.5 text-[10px] font-medium ${
                          r.status === 'Success'
                            ? 'bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400'
                            : 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'
                        }`}
                      >
                        {r.status}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="flex items-center justify-between">
            <Button
              variant="outline"
              size="sm"
              disabled={page <= 1}
              onClick={() => setPage(p => p - 1)}
            >
              Previous
            </Button>
            <span className="text-xs text-muted-foreground">Page {page}</span>
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
        <div className="rounded-xl border border-dashed border-border/60 p-12 text-center">
          <Activity className="mx-auto h-8 w-8 text-muted-foreground/50" />
          <p className="mt-3 text-sm text-muted-foreground">No AI requests found.</p>
        </div>
      )}
    </div>
  );
}
