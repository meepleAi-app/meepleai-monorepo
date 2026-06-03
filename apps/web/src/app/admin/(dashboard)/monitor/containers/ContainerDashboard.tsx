'use client';

/**
 * ContainerDashboard — Container status grid with SSE-driven refresh + polling fallback.
 * Issue #143  — Admin Infrastructure Panel Phase 4 (original polling implementation).
 * Issue #1853 — Wire `useLiveEvents` for real-time updates; demote polling to 60s+ fallback.
 * Issue #1837 — Lift `useLiveEvents` subscription to page.tsx so ContainerDashboard +
 *               LiveEventLog share a single EventSource. Accept `liveEvents` + `sseConnected`
 *               as props instead of subscribing internally.
 *
 * Refresh strategy:
 *   - SSE primary: parent owns `useLiveEvents({ aggregateTypes: ['Container', 'Infrastructure'] })`
 *     and forwards events here. New events trigger `fetchContainers()`.
 *   - Polling fallback: 60s default, settable up to 5m, guarantees state converges even when
 *     SSE is offline or the BE has not yet emitted relevant events.
 *
 * BE event-type gap (known, out of scope per #1853):
 *   The backend currently does not emit `Container.*` / `Infrastructure.*` domain
 *   events — `RestartServiceCommandHandler` only writes audit logs. The SSE filter
 *   above is a structural no-op until BE adds those event types, but the polling
 *   fallback keeps the UI accurate. When BE starts emitting events, no FE change is
 *   needed: the live-refresh path activates automatically.
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { Box, Clock, Loader2, Pause, Play, RadioTower, RefreshCw, ScrollText } from 'lucide-react';
import Link from 'next/link';

import type { DomainEventDto } from '@/components/admin/monitor/live-event-types';
import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';
import type { ContainerInfo } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

/* ------------------------------------------------------------------ */
/*  Constants                                                          */
/* ------------------------------------------------------------------ */

/**
 * Polling intervals — minimum 60s as #1853 demoted polling to a fallback path.
 * SSE handles real-time updates when BE emits Container/Infrastructure events.
 */
const REFRESH_OPTIONS = [
  { value: 60, label: '60s' },
  { value: 120, label: '2m' },
  { value: 300, label: '5m' },
] as const;

const DEFAULT_REFRESH_INTERVAL = 60;

/* ------------------------------------------------------------------ */
/*  Sub-components                                                     */
/* ------------------------------------------------------------------ */

function ContainerStatusBadge({ state }: { state: string }) {
  const variant =
    state === 'running' ? 'secondary' : state === 'exited' ? 'destructive' : 'outline';

  // SP5 #1837: token-aligned dot colors (was hardcoded bg-green/red/yellow-500).
  const dotColor =
    state === 'running' ? 'bg-emerald-500' : state === 'exited' ? 'bg-rose-500' : 'bg-amber-500';

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
      className="rounded-xl border bg-card/70 p-4 backdrop-blur-md transition-shadow hover:shadow-md"
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
          href={`/admin/monitor?tab=logs`}
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

  // SP5 #1837: token-aligned KPI strip (was green-600/red-600 hardcoded).
  return (
    <div className="grid grid-cols-3 gap-3" data-testid="status-summary">
      <div className="rounded-xl border border-border/60 bg-card/70 p-3 backdrop-blur-md">
        <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
          Total
        </p>
        <p className="font-quicksand text-lg font-bold text-foreground">{total}</p>
      </div>
      <div className="rounded-xl border border-emerald-500/30 bg-emerald-500/5 p-3 backdrop-blur-md">
        <p className="font-mono text-[10px] uppercase tracking-wider text-emerald-700 dark:text-emerald-300">
          Running
        </p>
        <p className="font-quicksand text-lg font-bold text-emerald-700 dark:text-emerald-300">
          {running}
        </p>
      </div>
      <div className="rounded-xl border border-rose-500/30 bg-rose-500/5 p-3 backdrop-blur-md">
        <p className="font-mono text-[10px] uppercase tracking-wider text-rose-700 dark:text-rose-300">
          Stopped
        </p>
        <p className="font-quicksand text-lg font-bold text-rose-700 dark:text-rose-300">
          {stopped}
        </p>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  Main Component                                                     */
/* ------------------------------------------------------------------ */

/**
 * Cap the polling backoff multiplier at 5× the base interval so the worst case
 * stays at 5 minutes (60s base × 5 = 300s). Beyond that admins should reach for
 * the manual refresh button or investigate why the BE is down.
 */
const MAX_BACKOFF_MULTIPLIER = 5;
const MAX_BACKOFF_DELAY_SECONDS = 300;

/**
 * Compute the next polling delay applying exponential backoff.
 *
 * Schedule (with baseSeconds=60):
 *   failures=0 → 60s  (1×, no backoff)
 *   failures=1 → 60s  (2^0=1×, first failure tolerated)
 *   failures=2 → 120s (2×)
 *   failures=3 → 240s (4×)
 *   failures≥4 → 300s (capped at MAX_BACKOFF_DELAY_SECONDS)
 *
 * Exported for unit testing — keeps the React component free of fake-timer
 * tests that are fragile against StrictMode + nested setTimeout chains.
 */
export function computePollingBackoff(
  baseSeconds: number,
  failures: number,
  caps: { maxMultiplier: number; maxDelaySeconds: number } = {
    maxMultiplier: MAX_BACKOFF_MULTIPLIER,
    maxDelaySeconds: MAX_BACKOFF_DELAY_SECONDS,
  }
): number {
  if (failures <= 0) return baseSeconds;
  const multiplier = Math.min(Math.pow(2, failures - 1), caps.maxMultiplier);
  return Math.min(baseSeconds * multiplier, caps.maxDelaySeconds);
}

interface ContainerDashboardProps {
  liveEvents?: ReadonlyArray<DomainEventDto>;
  sseConnected?: boolean;
}

export function ContainerDashboard({
  liveEvents = [],
  sseConnected = false,
}: ContainerDashboardProps = {}) {
  const [containers, setContainers] = useState<ContainerInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [refreshInterval, setRefreshInterval] = useState<number>(DEFAULT_REFRESH_INTERVAL);
  const [countdown, setCountdown] = useState<number>(DEFAULT_REFRESH_INTERVAL);

  const pollTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const countdownRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const hasLoadedRef = useRef(false);
  /**
   * Tracks consecutive `fetchContainers` failures so the polling fallback can
   * apply exponential backoff (1×, 2×, 4×, 5× cap). Reset to 0 on the first
   * successful fetch. Capped at 4 to bound the multiplier exponent.
   */
  const consecutiveFailuresRef = useRef(0);
  const { toast } = useToast();

  const fetchContainers = useCallback(async () => {
    try {
      const data = await api.admin.getDockerContainers();
      setContainers(data);
      hasLoadedRef.current = true;
      consecutiveFailuresRef.current = 0;
    } catch {
      consecutiveFailuresRef.current = Math.min(consecutiveFailuresRef.current + 1, 4);
      if (!hasLoadedRef.current) {
        toast({ title: 'Failed to load container data', variant: 'destructive' });
      }
    } finally {
      setLoading(false);
    }
  }, [toast]);

  /**
   * Bound the polling delay against the current `consecutiveFailuresRef`.
   * Uses the exported `computePollingBackoff` helper so the schedule stays
   * unit-testable in isolation.
   */
  const computeNextPollingDelay = useCallback(
    () => computePollingBackoff(refreshInterval, consecutiveFailuresRef.current),
    [refreshInterval]
  );

  // -----------------------------------------------------------------
  // #1853 / #1837 — SSE-driven refresh (primary path)
  // -----------------------------------------------------------------
  // The parent page owns the `useLiveEvents` subscription and forwards
  // events here. When the most-recent event id changes we trigger a
  // `fetchContainers()`. This avoids opening a second EventSource for
  // LiveEventLog and keeps a single source of truth for the SSE stream.
  const lastSeenEventIdRef = useRef<string | null>(null);

  useEffect(() => {
    const latest = liveEvents[0];
    if (!latest) return;
    if (latest.id === lastSeenEventIdRef.current) return;
    const previousId = lastSeenEventIdRef.current;
    lastSeenEventIdRef.current = latest.id;
    // Skip the very first observation (initial backfill on mount) — the
    // dedicated mount effect already triggers an HTTP `fetchContainers()`.
    if (previousId === null) return;
    void fetchContainers();
  }, [liveEvents, fetchContainers]);

  // -----------------------------------------------------------------
  // Initial fetch
  // -----------------------------------------------------------------
  useEffect(() => {
    fetchContainers();
  }, [fetchContainers]);

  // -----------------------------------------------------------------
  // Polling fallback (low-frequency: 60s minimum) with exponential backoff
  // -----------------------------------------------------------------
  // Two coordinated timers:
  //   1. `pollTimerRef` (setTimeout, recursive) — backoff-aware fetch scheduler.
  //      The next delay is computed by `computeNextPollingDelay()` so the
  //      `consecutiveFailuresRef` count drives the wait between polls.
  //   2. `countdownRef` (setInterval, 1s) — drives the visible "Xs" countdown.
  //      The updater is pure (no side effects), so StrictMode's double-invoke
  //      validation can't trigger extra fetches.
  useEffect(() => {
    if (pollTimerRef.current) clearTimeout(pollTimerRef.current);
    if (countdownRef.current) clearInterval(countdownRef.current);
    if (!autoRefresh) return;

    setCountdown(refreshInterval);

    countdownRef.current = setInterval(() => {
      // Pure updater — no fetch side effects (would double-fire under StrictMode).
      setCountdown(prev => (prev > 1 ? prev - 1 : computeNextPollingDelay()));
    }, 1000);

    const scheduleNextPoll = () => {
      const delaySeconds = computeNextPollingDelay();
      pollTimerRef.current = setTimeout(async () => {
        await fetchContainers();
        scheduleNextPoll(); // chain — next delay reflects updated failure count
      }, delaySeconds * 1000);
    };
    scheduleNextPoll();

    return () => {
      if (pollTimerRef.current) clearTimeout(pollTimerRef.current);
      if (countdownRef.current) clearInterval(countdownRef.current);
    };
  }, [autoRefresh, refreshInterval, fetchContainers, computeNextPollingDelay]);

  return (
    <div className="space-y-6" data-testid="container-dashboard">
      {/* Header Controls */}
      <div className="flex flex-wrap items-center gap-3">
        {/* SSE liveness indicator */}
        <Badge
          variant={sseConnected ? 'secondary' : 'outline'}
          className="gap-1.5 text-xs"
          data-testid="sse-status-indicator"
          aria-live="polite"
          aria-label={sseConnected ? 'Live updates connected' : 'Live updates disconnected'}
        >
          <RadioTower
            className={cn('h-3 w-3', sseConnected ? 'text-emerald-500' : 'text-muted-foreground')}
          />
          {sseConnected ? 'Live' : 'Polling only'}
        </Badge>

        {/* Auto-refresh toggle (polling fallback control) */}
        <div className="flex items-center gap-2" data-testid="auto-refresh-controls">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setAutoRefresh(!autoRefresh)}
            className="gap-1.5"
            data-testid="auto-refresh-toggle"
            aria-pressed={autoRefresh}
            aria-label={autoRefresh ? 'Pause auto-refresh' : 'Resume auto-refresh'}
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
            aria-label="Polling interval"
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
          className="rounded-xl border bg-card/70 p-8 text-center backdrop-blur-md"
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
