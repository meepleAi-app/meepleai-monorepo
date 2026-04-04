'use client';

/**
 * AppLogViewer Component
 * Structured application log viewer backed by Seq via the admin logs endpoint.
 */

import { useCallback, useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { ChevronDown, ChevronRight, RefreshCw, Search } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type {
  ApplicationLog,
  ApplicationLogsFilters,
} from '@/lib/api/schemas/admin/admin-logs.schemas';

const LOG_LEVELS = ['', 'Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal'] as const;

function levelBadgeClass(level: string): string {
  switch (level.toLowerCase()) {
    case 'information':
      return 'bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300';
    case 'warning':
      return 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300';
    case 'error':
      return 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300';
    case 'fatal':
      return 'bg-red-200 text-red-900 dark:bg-red-900/60 dark:text-red-200';
    case 'debug':
      return 'bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300';
    case 'verbose':
      return 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400';
    default:
      return 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400';
  }
}

function shortSource(source: string | null): string {
  if (!source) return '—';
  const parts = source.split('.');
  return parts[parts.length - 1] ?? source;
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

interface LogRowProps {
  log: ApplicationLog;
}

function LogRow({ log }: LogRowProps) {
  const [expanded, setExpanded] = useState(false);

  const hasDetails =
    log.correlationId ||
    log.source ||
    log.exception ||
    (log.properties && Object.keys(log.properties).length > 0);

  return (
    <>
      <tr
        className={`border-b transition-colors hover:bg-muted/40 ${expanded ? 'bg-muted/20' : ''}`}
        onClick={() => hasDetails && setExpanded(v => !v)}
        style={{ cursor: hasDetails ? 'pointer' : 'default' }}
        data-testid={`log-row-${log.id}`}
      >
        <td className="w-8 px-2 py-1.5 text-muted-foreground">
          {hasDetails ? (
            expanded ? (
              <ChevronDown className="h-3.5 w-3.5" />
            ) : (
              <ChevronRight className="h-3.5 w-3.5" />
            )
          ) : null}
        </td>
        <td className="whitespace-nowrap px-2 py-1.5 font-mono text-xs tabular-nums text-muted-foreground">
          {formatTimestamp(log.timestamp)}
        </td>
        <td className="px-2 py-1.5">
          <Badge className={`text-[10px] font-medium ${levelBadgeClass(log.level)}`}>
            {log.level}
          </Badge>
        </td>
        <td className="px-2 py-1.5 font-mono text-xs text-muted-foreground">
          {shortSource(log.source)}
        </td>
        <td className="max-w-xs truncate px-2 py-1.5 font-mono text-xs lg:max-w-lg xl:max-w-2xl">
          {log.message}
        </td>
      </tr>

      {expanded && hasDetails && (
        <tr className="border-b bg-muted/10">
          <td colSpan={5} className="px-6 py-3">
            <div className="space-y-2 font-mono text-xs">
              {log.correlationId && (
                <div>
                  <span className="font-semibold text-muted-foreground">Correlation ID: </span>
                  <span>{log.correlationId}</span>
                </div>
              )}
              {log.source && (
                <div>
                  <span className="font-semibold text-muted-foreground">Source: </span>
                  <span>{log.source}</span>
                </div>
              )}
              {log.properties && Object.keys(log.properties).length > 0 && (
                <div>
                  <div className="mb-1 font-semibold text-muted-foreground">Properties:</div>
                  <div className="ml-2 space-y-0.5">
                    {Object.entries(log.properties).map(([k, v]) => (
                      <div key={k}>
                        <span className="text-muted-foreground">{k}: </span>
                        <span>{v}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
              {log.exception && (
                <div>
                  <div className="mb-1 font-semibold text-red-600 dark:text-red-400">
                    Exception:
                  </div>
                  <pre className="overflow-auto whitespace-pre-wrap rounded bg-red-50 p-2 text-red-800 dark:bg-red-950/40 dark:text-red-300">
                    {log.exception}
                  </pre>
                </div>
              )}
            </div>
          </td>
        </tr>
      )}
    </>
  );
}

export function AppLogViewer() {
  const [filters, setFilters] = useState<ApplicationLogsFilters>({ count: 100 });
  const [search, setSearch] = useState('');
  const [level, setLevel] = useState('');
  const [source, setSource] = useState('');
  const [correlationId, setCorrelationId] = useState('');
  const [afterId, setAfterId] = useState<string | undefined>(undefined);

  const activeFilters: ApplicationLogsFilters = {
    ...filters,
    afterId,
  };

  const { data, isFetching, refetch } = useQuery({
    queryKey: ['admin', 'application-logs', activeFilters],
    queryFn: () => api.admin.getApplicationLogs(activeFilters),
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const handleApply = useCallback(() => {
    setAfterId(undefined);
    setFilters({
      search: search || undefined,
      level: level || undefined,
      source: source || undefined,
      correlationId: correlationId || undefined,
      count: 100,
    });
  }, [search, level, source, correlationId]);

  const handleRefresh = useCallback(() => {
    void refetch();
  }, [refetch]);

  const handleLoadMore = useCallback(() => {
    if (data?.lastId) {
      setAfterId(data.lastId);
    }
  }, [data?.lastId]);

  const items = data?.items ?? [];
  const remainingCount = data?.remainingCount ?? 0;

  return (
    <div
      className="rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70"
      data-testid="app-log-viewer"
    >
      {/* Filter bar */}
      <div className="flex flex-wrap items-end gap-2 border-b px-4 py-3">
        <div className="relative min-w-[180px] flex-1">
          <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search messages…"
            value={search}
            onChange={e => setSearch(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleApply()}
            className="pl-8 font-mono text-xs"
            data-testid="log-search-input"
          />
        </div>

        <Select value={level || '_all'} onValueChange={v => setLevel(v === '_all' ? '' : v)}>
          <SelectTrigger className="w-36 font-mono text-xs" data-testid="log-level-select">
            <SelectValue placeholder="All levels" />
          </SelectTrigger>
          <SelectContent>
            {LOG_LEVELS.map(l => (
              <SelectItem key={l || '_all'} value={l || '_all'} className="font-mono text-xs">
                {l || 'All levels'}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Input
          placeholder="Source filter…"
          value={source}
          onChange={e => setSource(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && handleApply()}
          className="w-40 font-mono text-xs"
          data-testid="log-source-input"
        />

        <Input
          placeholder="Correlation ID…"
          value={correlationId}
          onChange={e => setCorrelationId(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && handleApply()}
          className="w-44 font-mono text-xs"
          data-testid="log-correlation-input"
        />

        <Button size="sm" onClick={handleApply} data-testid="log-apply-btn">
          Apply
        </Button>

        <Button
          size="sm"
          variant="outline"
          onClick={handleRefresh}
          disabled={isFetching}
          data-testid="log-refresh-btn"
        >
          <RefreshCw className={`mr-1.5 h-3.5 w-3.5 ${isFetching ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {/* Log table */}
      <div className="overflow-auto">
        {isFetching && items.length === 0 ? (
          <div className="flex items-center justify-center py-12" data-testid="app-log-loading">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
          </div>
        ) : items.length === 0 ? (
          <div
            className="py-12 text-center text-sm text-muted-foreground"
            data-testid="app-log-empty"
          >
            No log entries found.
          </div>
        ) : (
          <table className="w-full text-sm" data-testid="app-log-table">
            <thead>
              <tr className="border-b bg-muted/30 text-left font-mono text-xs text-muted-foreground">
                <th className="w-8 px-2 py-2" />
                <th className="px-2 py-2">Time</th>
                <th className="px-2 py-2">Level</th>
                <th className="px-2 py-2">Source</th>
                <th className="px-2 py-2">Message</th>
              </tr>
            </thead>
            <tbody>
              {items.map(log => (
                <LogRow key={log.id} log={log} />
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Load more */}
      {remainingCount > 0 && data?.lastId && (
        <div className="flex items-center justify-between border-t px-4 py-3">
          <span className="text-xs text-muted-foreground">
            {remainingCount} more {remainingCount === 1 ? 'entry' : 'entries'} available
          </span>
          <Button
            size="sm"
            variant="outline"
            onClick={handleLoadMore}
            disabled={isFetching}
            data-testid="log-load-more-btn"
          >
            Load more
          </Button>
        </div>
      )}
    </div>
  );
}
