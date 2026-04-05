'use client';

import { useCallback } from 'react';

import { useQuery } from '@tanstack/react-query';
import { AlertCircle, RefreshCw } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/primitives/button';
import type { LokiLogEntry, LogsApiResponse } from '@/lib/loki/types';

function levelBadgeClass(level: LokiLogEntry['level']): string {
  switch (level) {
    case 'error':
      return 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300';
    case 'warning':
      return 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300';
    case 'info':
      return 'bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300';
    default:
      return 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400';
  }
}

function formatTimestamp(ts: string): string {
  try {
    return new Date(ts).toLocaleTimeString(undefined, {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  } catch {
    return ts;
  }
}

async function fetchLokiLogs(): Promise<LogsApiResponse> {
  const res = await fetch('/api/logs');
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json() as Promise<LogsApiResponse>;
}

export function LokiErrorViewer() {
  const { data, isFetching, isError, refetch } = useQuery({
    queryKey: ['admin', 'loki-errors'],
    queryFn: fetchLokiLogs,
    staleTime: 60_000,
    refetchInterval: 120_000,
  });

  const handleRefresh = useCallback(() => {
    void refetch();
  }, [refetch]);

  if (isFetching && !data) {
    return (
      <div className="flex items-center justify-center py-12" data-testid="loki-loading">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    );
  }

  if (isError) {
    return (
      <div
        className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-800 dark:bg-red-950/30 dark:text-red-400"
        data-testid="loki-error"
      >
        <AlertCircle className="h-4 w-4 shrink-0" />
        Unable to load container logs. Check your connection and try again.
      </div>
    );
  }

  if (data?.lokiUnavailable) {
    return (
      <div
        className="rounded-xl border bg-muted/30 p-6 text-center text-sm text-muted-foreground"
        data-testid="loki-unavailable"
      >
        <p className="font-medium">Loki log aggregation not available</p>
        <p className="mt-1 text-xs">
          Start the logging stack with <code className="rounded bg-muted px-1">make logging</code>{' '}
          on the staging server to enable container log collection.
        </p>
      </div>
    );
  }

  const entries = data?.entries ?? [];

  return (
    <div
      className="rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70"
      data-testid="loki-viewer"
    >
      {/* Header */}
      <div className="flex items-center justify-between border-b px-4 py-3">
        <span className="text-xs text-muted-foreground">Last 100 errors/warnings · last 24h</span>
        <Button
          size="sm"
          variant="outline"
          onClick={handleRefresh}
          disabled={isFetching}
          data-testid="loki-refresh-btn"
        >
          <RefreshCw className={`mr-1.5 h-3.5 w-3.5 ${isFetching ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {entries.length === 0 ? (
        <div className="py-12 text-center text-sm text-muted-foreground" data-testid="loki-empty">
          No errors or warnings in the last 24 hours.
        </div>
      ) : (
        <div className="overflow-auto">
          <table className="w-full text-sm" data-testid="loki-table">
            <thead>
              <tr className="border-b bg-muted/30 text-left font-mono text-xs text-muted-foreground">
                <th className="px-2 py-2">Time</th>
                <th className="px-2 py-2">Level</th>
                <th className="px-2 py-2">Container</th>
                <th className="px-2 py-2">Message</th>
              </tr>
            </thead>
            <tbody>
              {entries.map(entry => (
                <tr
                  key={entry.id}
                  className="border-b transition-colors hover:bg-muted/40"
                  data-testid={`loki-row-${entry.id}`}
                >
                  <td className="whitespace-nowrap px-2 py-1.5 font-mono text-xs tabular-nums text-muted-foreground">
                    {formatTimestamp(entry.timestamp)}
                  </td>
                  <td className="px-2 py-1.5">
                    <Badge className={`text-[10px] font-medium ${levelBadgeClass(entry.level)}`}>
                      {entry.level}
                    </Badge>
                  </td>
                  <td className="whitespace-nowrap px-2 py-1.5 font-mono text-xs text-muted-foreground">
                    {entry.container}
                  </td>
                  <td className="max-w-xs truncate px-2 py-1.5 font-mono text-xs lg:max-w-lg xl:max-w-2xl">
                    {entry.message}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
