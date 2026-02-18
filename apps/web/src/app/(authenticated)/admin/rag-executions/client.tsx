'use client';

import { useCallback, useEffect, useState } from 'react';

import {
  ActivityIcon,
  BarChart3Icon,
  ClockIcon,
  DollarSignIcon,
  FilterIcon,
  RefreshCwIcon,
  XIcon,
  ZapIcon,
} from 'lucide-react';

import { api } from '@/lib/api';
import type {
  RagExecutionDetail,
  RagExecutionListItem,
  RagExecutionStatsResult,
} from '@/lib/api/clients/adminClient';
import { cn } from '@/lib/utils';

type RagExecutionFilters = {
  strategy: string;
  status: string;
  minLatencyMs: string;
  maxLatencyMs: string;
  minConfidence: string;
  dateFrom: string;
  dateTo: string;
};

const STRATEGIES = ['', 'POC', 'SingleModel', 'TOMAC', 'HybridRAG'] as const;
const STATUSES = ['', 'Success', 'Error'] as const;
const PAGE_SIZE = 25;

export function RagExecutionHistoryClient() {
  const [executions, setExecutions] = useState<RagExecutionListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showFilters, setShowFilters] = useState(false);
  const [stats, setStats] = useState<RagExecutionStatsResult | null>(null);
  const [selectedExecution, setSelectedExecution] = useState<RagExecutionDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [filters, setFilters] = useState<RagExecutionFilters>({
    strategy: '',
    status: '',
    minLatencyMs: '',
    maxLatencyMs: '',
    minConfidence: '',
    dateFrom: '',
    dateTo: '',
  });

  const fetchExecutions = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.admin.getRagExecutions({
        skip: page * PAGE_SIZE,
        take: PAGE_SIZE,
        strategy: filters.strategy || undefined,
        status: filters.status || undefined,
        minLatencyMs: filters.minLatencyMs ? Number(filters.minLatencyMs) : undefined,
        maxLatencyMs: filters.maxLatencyMs ? Number(filters.maxLatencyMs) : undefined,
        minConfidence: filters.minConfidence ? Number(filters.minConfidence) : undefined,
        dateFrom: filters.dateFrom || undefined,
        dateTo: filters.dateTo || undefined,
      });
      setExecutions(result.items);
      setTotalCount(result.totalCount);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch RAG executions');
    } finally {
      setLoading(false);
    }
  }, [page, filters]);

  const fetchStats = useCallback(async () => {
    try {
      const result = await api.admin.getRagExecutionStats({
        dateFrom: filters.dateFrom || undefined,
        dateTo: filters.dateTo || undefined,
      });
      setStats(result);
    } catch {
      // Stats are non-critical, silently fail
    }
  }, [filters.dateFrom, filters.dateTo]);

  useEffect(() => {
    fetchExecutions();
    fetchStats();
  }, [fetchExecutions, fetchStats]);

  const handleRowClick = async (id: string) => {
    setDetailLoading(true);
    try {
      const detail = await api.admin.getRagExecutionById(id);
      setSelectedExecution(detail);
    } catch {
      setError('Failed to load execution details');
    } finally {
      setDetailLoading(false);
    }
  };

  const clearFilters = () => {
    setFilters({
      strategy: '',
      status: '',
      minLatencyMs: '',
      maxLatencyMs: '',
      minConfidence: '',
      dateFrom: '',
      dateTo: '',
    });
    setPage(0);
  };

  const totalPages = Math.ceil(totalCount / PAGE_SIZE);
  const hasActiveFilters = Object.values(filters).some(v => v !== '');

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <ActivityIcon className="h-6 w-6 text-muted-foreground" />
          <div>
            <h1 className="text-2xl font-bold tracking-tight">RAG Execution History</h1>
            <p className="text-sm text-muted-foreground">
              Monitor RAG pipeline executions and performance ({totalCount} executions)
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => { fetchExecutions(); fetchStats(); }}
            disabled={loading}
            className="inline-flex items-center gap-2 rounded-md border px-3 py-2 text-sm hover:bg-accent transition-colors"
          >
            <RefreshCwIcon className={cn('h-4 w-4', loading && 'animate-spin')} />
            Refresh
          </button>
          <button
            onClick={() => setShowFilters(!showFilters)}
            className={cn(
              'inline-flex items-center gap-2 rounded-md border px-3 py-2 text-sm hover:bg-accent transition-colors',
              hasActiveFilters && 'border-primary bg-primary/5'
            )}
          >
            <FilterIcon className="h-4 w-4" />
            Filters
            {hasActiveFilters && (
              <span className="rounded-full bg-primary px-1.5 py-0.5 text-[10px] text-primary-foreground">
                {Object.values(filters).filter(v => v !== '').length}
              </span>
            )}
          </button>
        </div>
      </div>

      {/* Stats Cards */}
      {stats && (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
          <StatCard
            icon={<ActivityIcon className="h-4 w-4" />}
            label="Total Executions"
            value={stats.totalExecutions.toLocaleString()}
          />
          <StatCard
            icon={<ClockIcon className="h-4 w-4" />}
            label="Avg Latency"
            value={`${Math.round(stats.avgLatencyMs)}ms`}
          />
          <StatCard
            icon={<ZapIcon className="h-4 w-4" />}
            label="Error Rate"
            value={`${(stats.errorRate * 100).toFixed(1)}%`}
            variant={stats.errorRate > 0.1 ? 'destructive' : 'default'}
          />
          <StatCard
            icon={<BarChart3Icon className="h-4 w-4" />}
            label="Cache Hit Rate"
            value={`${(stats.cacheHitRate * 100).toFixed(1)}%`}
          />
          <StatCard
            icon={<DollarSignIcon className="h-4 w-4" />}
            label="Total Cost"
            value={`$${stats.totalCost.toFixed(4)}`}
          />
          <StatCard
            icon={<BarChart3Icon className="h-4 w-4" />}
            label="Avg Confidence"
            value={`${(stats.avgConfidence * 100).toFixed(1)}%`}
          />
        </div>
      )}

      {/* Filters */}
      {showFilters && (
        <div className="rounded-lg border bg-card p-4 space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="font-medium text-sm">Filters</h3>
            {hasActiveFilters && (
              <button
                onClick={clearFilters}
                className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1"
              >
                <XIcon className="h-3 w-3" /> Clear all
              </button>
            )}
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Strategy</label>
              <select
                value={filters.strategy}
                onChange={e => { setFilters(f => ({ ...f, strategy: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              >
                <option value="">All Strategies</option>
                {STRATEGIES.filter(Boolean).map(s => (
                  <option key={s} value={s}>{s}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Status</label>
              <select
                value={filters.status}
                onChange={e => { setFilters(f => ({ ...f, status: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              >
                <option value="">All Statuses</option>
                {STATUSES.filter(Boolean).map(s => (
                  <option key={s} value={s}>{s}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Min Latency (ms)</label>
              <input
                type="number"
                placeholder="e.g. 100"
                value={filters.minLatencyMs}
                onChange={e => { setFilters(f => ({ ...f, minLatencyMs: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              />
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Max Latency (ms)</label>
              <input
                type="number"
                placeholder="e.g. 5000"
                value={filters.maxLatencyMs}
                onChange={e => { setFilters(f => ({ ...f, maxLatencyMs: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              />
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Min Confidence</label>
              <input
                type="number"
                step="0.1"
                min="0"
                max="1"
                placeholder="e.g. 0.7"
                value={filters.minConfidence}
                onChange={e => { setFilters(f => ({ ...f, minConfidence: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              />
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">From</label>
              <input
                type="date"
                value={filters.dateFrom}
                onChange={e => { setFilters(f => ({ ...f, dateFrom: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              />
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">To</label>
              <input
                type="date"
                value={filters.dateTo}
                onChange={e => { setFilters(f => ({ ...f, dateTo: e.target.value })); setPage(0); }}
                className="w-full rounded-md border bg-background px-3 py-1.5 text-sm"
              />
            </div>
          </div>
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="rounded-lg border border-destructive/50 bg-destructive/5 p-4 text-sm text-destructive">
          {error}
        </div>
      )}

      {/* Table */}
      <div className="rounded-lg border bg-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b bg-muted/50">
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Timestamp</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Query</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Strategy</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Model</th>
                <th className="px-4 py-3 text-left font-medium text-muted-foreground">Status</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">Latency</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">Tokens</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">Cost</th>
                <th className="px-4 py-3 text-right font-medium text-muted-foreground">Confidence</th>
              </tr>
            </thead>
            <tbody>
              {loading && executions.length === 0 ? (
                <tr>
                  <td colSpan={9} className="px-4 py-8 text-center text-muted-foreground">
                    Loading executions...
                  </td>
                </tr>
              ) : executions.length === 0 ? (
                <tr>
                  <td colSpan={9} className="px-4 py-8 text-center text-muted-foreground">
                    No RAG executions found
                  </td>
                </tr>
              ) : (
                executions.map(exec => (
                  <tr
                    key={exec.id}
                    onClick={() => handleRowClick(exec.id)}
                    className="border-b hover:bg-muted/30 cursor-pointer transition-colors"
                  >
                    <td className="px-4 py-3 whitespace-nowrap font-mono text-xs">
                      {new Date(exec.createdAt).toLocaleString()}
                    </td>
                    <td className="px-4 py-3 max-w-[200px] truncate" title={exec.query}>
                      {exec.query}
                    </td>
                    <td className="px-4 py-3">
                      <StrategyBadge strategy={exec.strategy} />
                    </td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">
                      {exec.model || '-'}
                    </td>
                    <td className="px-4 py-3">
                      <StatusBadge status={exec.status} />
                    </td>
                    <td className="px-4 py-3 text-right font-mono text-xs">
                      <LatencyDisplay ms={exec.totalLatencyMs} />
                    </td>
                    <td className="px-4 py-3 text-right font-mono text-xs">
                      {exec.totalTokens.toLocaleString()}
                    </td>
                    <td className="px-4 py-3 text-right font-mono text-xs">
                      ${exec.totalCost.toFixed(4)}
                    </td>
                    <td className="px-4 py-3 text-right font-mono text-xs">
                      {exec.confidence !== null && exec.confidence !== undefined ? `${(exec.confidence * 100).toFixed(0)}%` : '-'}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between border-t px-4 py-3">
            <p className="text-xs text-muted-foreground">
              Showing {page * PAGE_SIZE + 1}-{Math.min((page + 1) * PAGE_SIZE, totalCount)} of {totalCount}
            </p>
            <div className="flex items-center gap-1">
              <button
                onClick={() => setPage(p => Math.max(0, p - 1))}
                disabled={page === 0}
                className="rounded-md border px-3 py-1 text-xs disabled:opacity-50 hover:bg-accent"
              >
                Previous
              </button>
              <span className="px-2 text-xs text-muted-foreground">
                Page {page + 1} of {totalPages}
              </span>
              <button
                onClick={() => setPage(p => Math.min(totalPages - 1, p + 1))}
                disabled={page >= totalPages - 1}
                className="rounded-md border px-3 py-1 text-xs disabled:opacity-50 hover:bg-accent"
              >
                Next
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Detail Modal */}
      {(selectedExecution || detailLoading) && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
          onClick={() => { if (!detailLoading) setSelectedExecution(null); }}
        >
          <div
            className="w-full max-w-2xl max-h-[80vh] overflow-y-auto rounded-lg border bg-card p-6 shadow-lg mx-4"
            onClick={e => e.stopPropagation()}
          >
            {detailLoading ? (
              <p className="text-center text-muted-foreground py-8">Loading details...</p>
            ) : selectedExecution ? (
              <>
                <div className="flex items-center justify-between mb-4">
                  <h3 className="font-semibold">Execution Detail</h3>
                  <button
                    onClick={() => setSelectedExecution(null)}
                    className="rounded-md p-1 hover:bg-muted"
                  >
                    <XIcon className="h-4 w-4" />
                  </button>
                </div>
                <dl className="space-y-3 text-sm">
                  <DetailRow label="ID" value={selectedExecution.id} mono />
                  <DetailRow label="Timestamp" value={new Date(selectedExecution.createdAt).toLocaleString()} />
                  <DetailRow label="Query" value={selectedExecution.query} />
                  <DetailRow label="Agent" value={selectedExecution.agentName || '-'} />
                  <DetailRow label="Strategy" value={selectedExecution.strategy} />
                  <DetailRow label="Model" value={selectedExecution.model || '-'} />
                  <DetailRow label="Provider" value={selectedExecution.provider || '-'} />
                  <DetailRow label="Status" value={selectedExecution.status} />
                  {selectedExecution.errorMessage && (
                    <DetailRow label="Error" value={selectedExecution.errorMessage} />
                  )}
                  <DetailRow label="Total Latency" value={`${selectedExecution.totalLatencyMs}ms`} />
                  <DetailRow label="Prompt Tokens" value={selectedExecution.promptTokens.toLocaleString()} />
                  <DetailRow label="Completion Tokens" value={selectedExecution.completionTokens.toLocaleString()} />
                  <DetailRow label="Total Tokens" value={selectedExecution.totalTokens.toLocaleString()} />
                  <DetailRow label="Total Cost" value={`$${selectedExecution.totalCost.toFixed(6)}`} />
                  <DetailRow
                    label="Confidence"
                    value={selectedExecution.confidence !== null && selectedExecution.confidence !== undefined ? `${(selectedExecution.confidence * 100).toFixed(1)}%` : '-'}
                  />
                  <DetailRow label="Cache Hit" value={selectedExecution.cacheHit ? 'Yes' : 'No'} />
                  <DetailRow label="Playground" value={selectedExecution.isPlayground ? 'Yes' : 'No'} />
                  {selectedExecution.executionTrace && (
                    <div>
                      <dt className="text-muted-foreground mb-1">Execution Trace</dt>
                      <dd className="rounded-md bg-muted p-3 font-mono text-xs whitespace-pre-wrap break-all max-h-64 overflow-y-auto">
                        {formatTrace(selectedExecution.executionTrace)}
                      </dd>
                    </div>
                  )}
                </dl>
              </>
            ) : null}
          </div>
        </div>
      )}
    </div>
  );
}

function StatCard({
  icon,
  label,
  value,
  variant = 'default',
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  variant?: 'default' | 'destructive';
}) {
  return (
    <div className="rounded-lg border bg-card p-4">
      <div className="flex items-center gap-2 text-xs text-muted-foreground mb-1">
        {icon}
        {label}
      </div>
      <div className={cn(
        'text-xl font-bold',
        variant === 'destructive' && 'text-destructive'
      )}>
        {value}
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const colors: Record<string, string> = {
    Success: 'bg-green-500/10 text-green-700 dark:text-green-400',
    Error: 'bg-red-500/10 text-red-700 dark:text-red-400',
  };

  return (
    <span className={cn(
      'inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium',
      colors[status] || 'bg-muted text-muted-foreground'
    )}>
      {status}
    </span>
  );
}

function StrategyBadge({ strategy }: { strategy: string }) {
  const colors: Record<string, string> = {
    POC: 'bg-blue-500/10 text-blue-700 dark:text-blue-400',
    SingleModel: 'bg-blue-500/10 text-blue-700 dark:text-blue-400',
    TOMAC: 'bg-purple-500/10 text-purple-700 dark:text-purple-400',
    HybridRAG: 'bg-amber-500/10 text-amber-700 dark:text-amber-400',
  };

  return (
    <span className={cn(
      'inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium',
      colors[strategy] || 'bg-muted text-muted-foreground'
    )}>
      {strategy}
    </span>
  );
}

function LatencyDisplay({ ms }: { ms: number }) {
  const color = ms > 5000
    ? 'text-red-600 dark:text-red-400'
    : ms > 2000
      ? 'text-amber-600 dark:text-amber-400'
      : 'text-green-600 dark:text-green-400';

  return <span className={color}>{ms.toLocaleString()}ms</span>;
}

function DetailRow({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="text-muted-foreground shrink-0">{label}</dt>
      <dd className={cn('text-right', mono && 'font-mono text-xs')}>{value}</dd>
    </div>
  );
}

function formatTrace(trace: string): string {
  try {
    return JSON.stringify(JSON.parse(trace), null, 2);
  } catch {
    return trace;
  }
}
