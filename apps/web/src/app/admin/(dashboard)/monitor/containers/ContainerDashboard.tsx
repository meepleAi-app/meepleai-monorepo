'use client';

/**
 * ContainerDashboard — Container status grid with auto-refresh, metrics, and action buttons.
 * Issue #143 — Admin Infrastructure Panel Phase 4
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { Box, Clock, Loader2, Pause, Play, RefreshCw, ScrollText } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/use-toast';
import { api } from '@/lib/api';
import type { ContainerInfo } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

/* ------------------------------------------------------------------ */
/*  Constants                                                          */
/* ------------------------------------------------------------------ */

const REFRESH_OPTIONS = [
  { value: 15, label: '15s' },
  { value: 30, label: '30s' },
  { value: 60, label: '60s' },
] as const;

/* ------------------------------------------------------------------ */
/*  Sub-components                                                     */
/* ------------------------------------------------------------------ */

function ContainerStatusBadge({ state }: { state: string }) {
  const variant =
    state === 'running' ? 'secondary' : state === 'exited' ? 'destructive' : 'outline';

  const dotColor =
    state === 'running' ? 'bg-green-500' : state === 'exited' ? 'bg-red-500' : 'bg-yellow-500';

  return (
    <Badge variant={variant} className="gap-1.5 text-xs" data-testid="container-status-badge">
      <span className={cn('inline-block h-2 w-2 rounded-full', dotColor)} />
      {state}
    </Badge>
  );
}

function formatUptime(container: ContainerInfo): string {
  if (container.state !== 'running') return '—';
  const diff = Date.now() - new Date(container.created).getTime();
  const hours = Math.floor(diff / (1000 * 60 * 60));
  const days = Math.floor(hours / 24);

  if (days > 0) return `${days}d ${hours % 24}h`;
  if (hours > 0) return `${hours}h`;
  const minutes = Math.floor(diff / (1000 * 60));
  return `${minutes}m`;
}

interface ContainerCardProps {
  container: ContainerInfo;
}

function ContainerCard({ container }: ContainerCardProps) {
  return (
    <div
      data-testid={`container-card-${container.id}`}
      className="rounded-xl border bg-white/70 p-4 backdrop-blur-md transition-shadow hover:shadow-md dark:bg-zinc-900/70"
    >
      {/* Header */}
      <div className="mb-3 flex items-start justify-between">
        <div className="min-w-0 flex-1">
          <h3 className="truncate text-sm font-semibold text-foreground">{container.name}</h3>
          <p className="mt-0.5 truncate text-xs text-muted-foreground">{container.image}</p>
        </div>
        <ContainerStatusBadge state={container.state} />
      </div>

      {/* Metrics row */}
      <div className="mb-3 grid grid-cols-2 gap-2">
        <div className="rounded-lg bg-muted/50 px-2.5 py-1.5">
          <p className="text-[10px] uppercase tracking-wider text-muted-foreground">Status</p>
          <p className="text-xs font-medium" data-testid="container-status-text">
            {container.status}
          </p>
        </div>
        <div className="rounded-lg bg-muted/50 px-2.5 py-1.5">
          <p className="text-[10px] uppercase tracking-wider text-muted-foreground">Uptime</p>
          <p className="text-xs font-medium" data-testid="container-uptime">
            {formatUptime(container)}
          </p>
        </div>
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2">
        <Link
          href={`/admin/monitor/logs`}
          className="inline-flex items-center gap-1 rounded-md px-2.5 py-1.5 text-xs font-medium text-primary hover:bg-primary/10 transition-colors"
          data-testid={`view-logs-${container.id}`}
        >
          <ScrollText className="h-3 w-3" />
          View Logs
        </Link>
      </div>
    </div>
  );
}

function StatusSummary({ containers }: { containers: ContainerInfo[] }) {
  const running = containers.filter(c => c.state === 'running').length;
  const stopped = containers.filter(c => c.state !== 'running').length;
  const total = containers.length;

  return (
    <div className="grid grid-cols-3 gap-3" data-testid="status-summary">
      <div className="rounded-xl border bg-white/70 p-3 backdrop-blur-md dark:bg-zinc-900/70">
        <p className="text-xs text-muted-foreground">Total</p>
        <p className="text-lg font-semibold font-mono">{total}</p>
      </div>
      <div className="rounded-xl border bg-white/70 p-3 backdrop-blur-md dark:bg-zinc-900/70">
        <p className="text-xs text-green-600">Running</p>
        <p className="text-lg font-semibold font-mono text-green-600">{running}</p>
      </div>
      <div className="rounded-xl border bg-white/70 p-3 backdrop-blur-md dark:bg-zinc-900/70">
        <p className="text-xs text-red-600">Stopped</p>
        <p className="text-lg font-semibold font-mono text-red-600">{stopped}</p>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  Main Component                                                     */
/* ------------------------------------------------------------------ */

export function ContainerDashboard() {
  const [containers, setContainers] = useState<ContainerInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [refreshInterval, setRefreshInterval] = useState(30);
  const [countdown, setCountdown] = useState(30);

  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const countdownRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const hasLoadedRef = useRef(false);
  const { toast } = useToast();

  const fetchContainers = useCallback(async () => {
    try {
      const data = await api.admin.getDockerContainers();
      setContainers(data);
      hasLoadedRef.current = true;
    } catch {
      if (!hasLoadedRef.current) {
        toast({ title: 'Failed to load container data', variant: 'destructive' });
      }
    } finally {
      setLoading(false);
    }
  }, [toast]);

  // Initial fetch
  useEffect(() => {
    fetchContainers();
  }, [fetchContainers]);

  // Auto-refresh interval
  useEffect(() => {
    if (intervalRef.current) clearInterval(intervalRef.current);
    if (countdownRef.current) clearInterval(countdownRef.current);

    if (autoRefresh) {
      setCountdown(refreshInterval);

      countdownRef.current = setInterval(() => {
        setCountdown(prev => {
          if (prev <= 1) return refreshInterval;
          return prev - 1;
        });
      }, 1000);

      intervalRef.current = setInterval(() => {
        fetchContainers();
        setCountdown(refreshInterval);
      }, refreshInterval * 1000);
    }

    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
      if (countdownRef.current) clearInterval(countdownRef.current);
    };
  }, [autoRefresh, refreshInterval, fetchContainers]);

  return (
    <div className="space-y-6" data-testid="container-dashboard">
      {/* Header Controls */}
      <div className="flex flex-wrap items-center gap-3">
        {/* Auto-refresh toggle */}
        <div className="flex items-center gap-2" data-testid="auto-refresh-controls">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setAutoRefresh(!autoRefresh)}
            className="gap-1.5"
            data-testid="auto-refresh-toggle"
          >
            {autoRefresh ? <Pause className="h-3.5 w-3.5" /> : <Play className="h-3.5 w-3.5" />}
            {autoRefresh ? 'Pause' : 'Resume'}
          </Button>

          {autoRefresh && (
            <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <Clock className="h-3 w-3" />
              <span data-testid="countdown">{countdown}s</span>
            </div>
          )}

          <select
            value={refreshInterval}
            onChange={e => {
              setRefreshInterval(Number(e.target.value));
              setCountdown(Number(e.target.value));
            }}
            className="rounded-md border bg-background px-2 py-1 text-xs"
            data-testid="refresh-interval-select"
          >
            {REFRESH_OPTIONS.map(opt => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Manual refresh */}
        <Button
          variant="outline"
          size="sm"
          onClick={() => fetchContainers()}
          className="gap-1.5 ml-auto"
          data-testid="manual-refresh"
        >
          <RefreshCw className="h-3.5 w-3.5" />
          Refresh
        </Button>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-10" data-testid="container-loading">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : containers.length === 0 ? (
        <div
          data-testid="container-empty"
          className="rounded-xl border bg-white/70 p-8 text-center backdrop-blur-md dark:bg-zinc-900/70"
        >
          <Box className="mx-auto mb-3 h-8 w-8 text-muted-foreground" />
          <p className="text-muted-foreground">
            No containers found. Make sure Docker Socket Proxy is running.
          </p>
        </div>
      ) : (
        <>
          {/* Status Summary */}
          <StatusSummary containers={containers} />

          {/* Container Grid */}
          <div
            className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3"
            data-testid="container-grid"
          >
            {containers.map(container => (
              <ContainerCard key={container.id} container={container} />
            ))}
          </div>
        </>
      )}
    </div>
  );
}
