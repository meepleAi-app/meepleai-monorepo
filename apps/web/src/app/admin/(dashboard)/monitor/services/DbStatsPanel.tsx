'use client';

/**
 * DbStatsPanel — Database statistics overview panel showing size, connections,
 * transactions, top tables, and vacuum controls.
 * Issue #135 — DB Stats Overview for Service Dashboard
 */

import { useCallback, useEffect, useState } from 'react';

import {
  ChevronDown,
  ChevronRight,
  Database,
  HardDrive,
  Loader2,
  RefreshCw,
  Table2,
  TrendingDown,
  TrendingUp,
  Zap,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';
import type { DatabaseMetrics, TableSize } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

function formatRelativeTime(isoDate: string): string {
  const diff = Date.now() - new Date(isoDate).getTime();
  const seconds = Math.floor(diff / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${units[i]}`;
}

function GrowthIndicator({ bytes, label }: { bytes: number; label: string }) {
  const isPositive = bytes > 0;
  const isZero = bytes === 0;

  return (
    <span
      className={cn(
        'inline-flex items-center gap-0.5 text-xs',
        isZero
          ? 'text-muted-foreground'
          : isPositive
            ? 'text-amber-600 dark:text-amber-400'
            : 'text-green-600 dark:text-green-400'
      )}
      title={`${label}: ${isPositive ? '+' : ''}${formatBytes(bytes)}`}
    >
      {!isZero &&
        (isPositive ? <TrendingUp className="h-3 w-3" /> : <TrendingDown className="h-3 w-3" />)}
      <span>
        {label}: {isPositive ? '+' : ''}
        {formatBytes(Math.abs(bytes))}
      </span>
    </span>
  );
}

function ConnectionBar({ active, max }: { active: number; max: number }) {
  const percent = max > 0 ? (active / max) * 100 : 0;
  const barColor = percent >= 80 ? 'bg-red-500' : percent >= 60 ? 'bg-amber-500' : 'bg-green-500';

  return (
    <div className="space-y-1">
      <div className="flex items-baseline justify-between">
        <span className="text-lg font-semibold font-mono">
          {active}
          <span className="text-xs text-muted-foreground font-normal"> / {max}</span>
        </span>
      </div>
      <div
        className="h-1.5 w-full rounded-full bg-slate-200 dark:bg-zinc-700 overflow-hidden"
        data-testid="db-connections-bar"
      >
        <div
          className={cn('h-full rounded-full transition-all duration-500', barColor)}
          style={{ width: `${Math.min(percent, 100)}%` }}
        />
      </div>
      <p className="text-xs text-muted-foreground">{percent.toFixed(1)}% utilized</p>
    </div>
  );
}

function TopTablesTable({ tables }: { tables: TableSize[] }) {
  const [open, setOpen] = useState(true);

  return (
    <div
      className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md overflow-hidden"
      data-testid="db-top-tables"
    >
      <button
        className="w-full flex items-center justify-between px-4 py-3 hover:bg-slate-50/50 dark:hover:bg-zinc-800/30 transition-colors"
        onClick={() => setOpen(!open)}
        data-testid="db-top-tables-toggle"
      >
        <div className="flex items-center gap-2">
          {open ? (
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          )}
          <Table2 className="h-4 w-4 text-blue-600" />
          <span className="font-quicksand font-semibold text-sm">Top Tables</span>
          <span className="text-xs text-muted-foreground">({tables.length})</span>
        </div>
      </button>
      {open && (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-t border-b text-xs text-muted-foreground">
                <th className="text-left py-2 px-4">Table Name</th>
                <th className="text-right py-2 px-3">Rows</th>
                <th className="text-right py-2 px-3">Data Size</th>
                <th className="text-right py-2 px-3">Index Size</th>
                <th className="text-right py-2 px-3">Total Size</th>
              </tr>
            </thead>
            <tbody>
              {tables.map(table => (
                <tr
                  key={table.tableName}
                  className="border-b last:border-0"
                  data-testid={`db-table-row-${table.tableName}`}
                >
                  <td className="py-2.5 px-4">
                    <span className="font-mono text-xs">{table.tableName}</span>
                  </td>
                  <td className="py-2.5 px-3 text-right font-mono text-xs">
                    {table.rowCount.toLocaleString()}
                  </td>
                  <td className="py-2.5 px-3 text-right font-mono text-xs">
                    {table.sizeFormatted}
                  </td>
                  <td className="py-2.5 px-3 text-right font-mono text-xs">
                    {table.indexSizeFormatted}
                  </td>
                  <td className="py-2.5 px-3 text-right font-mono text-xs font-medium">
                    {table.totalSizeFormatted}
                  </td>
                </tr>
              ))}
              {tables.length === 0 && (
                <tr>
                  <td colSpan={5} className="py-6 text-center text-sm text-muted-foreground">
                    No table data available.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export function DbStatsPanel() {
  const [metrics, setMetrics] = useState<DatabaseMetrics | null>(null);
  const [tables, setTables] = useState<TableSize[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [vacuuming, setVacuuming] = useState(false);
  const [vacuumConfirm, setVacuumConfirm] = useState(false);
  const { toast } = useToast();

  const fetchData = useCallback(async () => {
    try {
      const [metricsResult, tablesResult] = await Promise.all([
        api.admin.getResourceDatabaseMetrics(),
        api.admin.getResourceDatabaseTopTables(10),
      ]);
      setMetrics(metricsResult);
      setTables(tablesResult);
    } catch {
      toast({ title: 'Failed to load database metrics', variant: 'destructive' });
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [toast]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleRefresh = useCallback(() => {
    setRefreshing(true);
    fetchData();
  }, [fetchData]);

  const handleVacuum = useCallback(async () => {
    setVacuumConfirm(false);
    setVacuuming(true);
    try {
      await api.admin.vacuumDatabase(false);
      toast({ title: 'Vacuum completed successfully' });
      fetchData();
    } catch {
      toast({ title: 'Vacuum failed', variant: 'destructive' });
    } finally {
      setVacuuming(false);
    }
  }, [toast, fetchData]);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-10" data-testid="db-stats-loading">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!metrics) {
    return (
      <div className="text-center py-10 text-sm text-muted-foreground" data-testid="db-stats-empty">
        No database metrics available.
      </div>
    );
  }

  const txTotal = metrics.transactionsCommitted + metrics.transactionsRolledBack;
  const txSuccessRate = txTotal > 0 ? (metrics.transactionsCommitted / txTotal) * 100 : 100;

  return (
    <div className="space-y-4" data-testid="db-stats-panel">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Database className="h-5 w-5 text-blue-600" />
          <h3 className="font-quicksand font-semibold text-base">Database Overview</h3>
        </div>
        <div className="flex items-center gap-2">
          {vacuumConfirm ? (
            <div className="flex items-center gap-1.5" data-testid="db-vacuum-confirm">
              <span className="text-xs text-destructive font-medium">Run VACUUM?</span>
              <Button
                variant="destructive"
                size="sm"
                onClick={handleVacuum}
                disabled={vacuuming}
                className="gap-1 h-7 text-xs"
                data-testid="db-vacuum-confirm-yes"
              >
                {vacuuming ? (
                  <Loader2 className="h-3 w-3 animate-spin" />
                ) : (
                  <Zap className="h-3 w-3" />
                )}
                Yes
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setVacuumConfirm(false)}
                className="h-7 text-xs"
                data-testid="db-vacuum-confirm-no"
              >
                Cancel
              </Button>
            </div>
          ) : (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setVacuumConfirm(true)}
              disabled={vacuuming}
              className="gap-1.5"
              data-testid="db-vacuum-btn"
            >
              {vacuuming ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : (
                <Zap className="h-3.5 w-3.5" />
              )}
              Vacuum
            </Button>
          )}
          <Button
            variant="outline"
            size="sm"
            onClick={handleRefresh}
            disabled={refreshing}
            className="gap-1.5"
            data-testid="db-refresh-btn"
          >
            <RefreshCw className={cn('h-3.5 w-3.5', refreshing && 'animate-spin')} />
            Refresh
          </Button>
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3" data-testid="db-kpi-row">
        {/* Total Size */}
        <div
          className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md p-3"
          data-testid="db-kpi-size"
        >
          <div className="flex items-center gap-1.5 mb-1">
            <HardDrive className="h-3.5 w-3.5 text-muted-foreground" />
            <p className="text-xs text-muted-foreground">Total Size</p>
          </div>
          <p className="text-lg font-semibold font-mono">{metrics.sizeFormatted}</p>
          <div className="flex flex-col gap-0.5 mt-1.5">
            <GrowthIndicator bytes={metrics.growthLast7Days} label="7d" />
            <GrowthIndicator bytes={metrics.growthLast30Days} label="30d" />
            <GrowthIndicator bytes={metrics.growthLast90Days} label="90d" />
          </div>
        </div>

        {/* Active Connections */}
        <div
          className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md p-3"
          data-testid="db-kpi-connections"
        >
          <p className="text-xs text-muted-foreground mb-1">Active Connections</p>
          <ConnectionBar active={metrics.activeConnections} max={metrics.maxConnections} />
        </div>

        {/* Transactions */}
        <div
          className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md p-3"
          data-testid="db-kpi-transactions"
        >
          <p className="text-xs text-muted-foreground mb-1">Transactions</p>
          <p className="text-lg font-semibold font-mono">{txTotal.toLocaleString()}</p>
          <div className="flex items-center gap-2 mt-1.5">
            <Badge variant="secondary" className="text-xs">
              {txSuccessRate.toFixed(1)}% success
            </Badge>
          </div>
          <div className="flex gap-3 mt-1 text-xs text-muted-foreground">
            <span>{metrics.transactionsCommitted.toLocaleString()} committed</span>
            {metrics.transactionsRolledBack > 0 && (
              <span className="text-red-500">
                {metrics.transactionsRolledBack.toLocaleString()} rolled back
              </span>
            )}
          </div>
        </div>

        {/* Last Measured */}
        <div
          className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md p-3"
          data-testid="db-kpi-measured"
        >
          <p className="text-xs text-muted-foreground mb-1">Last Measured</p>
          <p className="text-lg font-semibold font-mono">
            {formatRelativeTime(metrics.measuredAt)}
          </p>
          <p className="text-xs text-muted-foreground mt-1.5">
            {new Date(metrics.measuredAt).toLocaleString()}
          </p>
        </div>
      </div>

      {/* Top Tables */}
      <TopTablesTable tables={tables} />
    </div>
  );
}
