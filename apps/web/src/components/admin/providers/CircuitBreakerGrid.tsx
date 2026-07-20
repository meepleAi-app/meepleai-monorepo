/* eslint-disable local/no-hardcoded-color-utility -- admin circuit breaker grid: emerald/amber/rose status palette + zinc default fallback (admin convention DS-13c, scope deferred to DS-16) */
'use client';

/**
 * #1834 SP5 F4-C3 — CircuitBreakerGrid
 *
 * Grid degli stati Polly circuit breaker per servizi LLM
 * (mockup `sp5-admin-providers.html` §4).
 *
 * PR2: layout 2×2 stat-grid (mockup-aligned) + cooldown bar countdown per
 * stati open/half-open. Cooldown derivato da
 * `useLlmSystemConfig().circuitBreakerOpenDurationSeconds` + `lastTrippedAt`.
 *
 * Stato visivo:
 * - `Closed` → emerald (verde) — flow normale
 * - `Open` → rose (rosso) — circuit aperto, cooldown countdown attivo
 * - `HalfOpen` → amber (giallo) — recupero in corso
 */

import { useEffect, useState } from 'react';

import { useCircuitBreakerStates, useLlmSystemConfig } from '@/hooks/queries/useProviders';
import type { CircuitBreakerState } from '@/lib/api/schemas/admin/admin-circuit-breakers.schemas';

function stateClass(state: string): string {
  switch (state.toLowerCase()) {
    case 'closed':
      return 'border-emerald-500/30 bg-emerald-500/5';
    case 'open':
      return 'border-rose-500/40 bg-rose-500/5';
    case 'half-open':
    case 'halfopen':
      return 'border-amber-500/30 bg-amber-500/5';
    default:
      return 'border-zinc-500/30 bg-zinc-500/5';
  }
}

function chipClass(state: string): string {
  switch (state.toLowerCase()) {
    case 'closed':
      return 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/40';
    case 'open':
      return 'bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/40';
    case 'half-open':
    case 'halfopen':
      return 'bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/40';
    default:
      return 'bg-zinc-500/15 text-zinc-700 dark:text-zinc-300 border-zinc-500/40';
  }
}

function isCooldownState(state: string): boolean {
  const s = state.toLowerCase();
  return s === 'open' || s === 'half-open' || s === 'halfopen';
}

function formatRelative(iso: string): string {
  const then = new Date(iso).getTime();
  const now = Date.now();
  const diffSec = Math.max(0, Math.floor((now - then) / 1000));
  if (diffSec < 60) return `${diffSec}s fa`;
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `${diffMin}m fa`;
  const diffH = Math.floor(diffMin / 60);
  if (diffH < 24) return `${diffH}h fa`;
  const diffD = Math.floor(diffH / 24);
  return `${diffD}g fa`;
}

interface CooldownBarProps {
  readonly trippedAtIso: string;
  readonly cooldownSec: number;
}

/**
 * Cooldown bar with live countdown. Updates every 1s via state tick.
 * Stops re-rendering once countdown completes.
 */
function CooldownBar({ trippedAtIso, cooldownSec }: CooldownBarProps) {
  const computeRemaining = () => {
    const trippedMs = new Date(trippedAtIso).getTime();
    const elapsed = Math.max(0, (Date.now() - trippedMs) / 1000);
    return Math.max(0, cooldownSec - elapsed);
  };
  const [remaining, setRemaining] = useState(computeRemaining);

  useEffect(() => {
    if (remaining <= 0) return;
    const id = setInterval(() => {
      const next = computeRemaining();
      setRemaining(next);
      // #3231: stop the interval once the countdown hits zero — the effect deps don't include
      // `remaining`, so without this the timer would tick at 1 Hz forever.
      if (next <= 0) clearInterval(id);
    }, 1000);
    return () => clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trippedAtIso, cooldownSec]);

  const pct = cooldownSec > 0 ? Math.max(0, Math.min(100, (remaining / cooldownSec) * 100)) : 0;

  return (
    <div className="mt-1" data-testid="cb-cooldown-bar">
      <div
        className="h-1.5 w-full overflow-hidden rounded-full bg-muted/60 dark:bg-zinc-800"
        role="progressbar"
        aria-label="Cooldown timer residuo"
        aria-valuenow={Math.round(remaining)}
        aria-valuemin={0}
        aria-valuemax={cooldownSec}
      >
        <div
          className="h-full rounded-full bg-amber-500 transition-[width] duration-500 ease-linear"
          style={{ width: `${pct}%` }}
        />
      </div>
      <div className="mt-0.5 flex justify-between font-mono text-[9.5px] text-muted-foreground">
        <span>cooldown</span>
        <span
          className="font-bold text-amber-700 dark:text-amber-300"
          data-testid="cb-cooldown-remaining"
        >
          {Math.ceil(remaining)}s
        </span>
      </div>
    </div>
  );
}

interface StatItemProps {
  readonly label: string;
  readonly value: string | number;
  readonly tone?: 'default' | 'warn' | 'muted';
}

function StatItem({ label, value, tone = 'default' }: StatItemProps) {
  const toneClass: Record<NonNullable<StatItemProps['tone']>, string> = {
    default: 'text-foreground',
    warn: 'text-amber-700 dark:text-amber-300',
    muted: 'text-muted-foreground',
  };
  return (
    <div className="flex flex-col gap-px">
      <span className="font-mono text-[9px] uppercase tracking-wider text-muted-foreground font-bold">
        {label}
      </span>
      <span className={`font-mono text-xs font-bold tabular-nums ${toneClass[tone]}`}>{value}</span>
    </div>
  );
}

interface CircuitCardProps {
  readonly breaker: CircuitBreakerState;
  readonly cooldownSec: number | undefined;
  readonly failureThreshold: number | undefined;
}

function CircuitCard({ breaker, cooldownSec, failureThreshold }: CircuitCardProps) {
  const showCooldown =
    isCooldownState(breaker.state) && breaker.lastTrippedAt !== null && cooldownSec != null;

  return (
    <div
      data-testid={`circuit-card-${breaker.serviceName}`}
      className={`rounded-lg border p-3 flex flex-col gap-2 ${stateClass(breaker.state)}`}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div
            className="font-quicksand text-sm font-bold text-foreground truncate"
            title={breaker.serviceName}
          >
            {breaker.serviceName}
          </div>
          <div className="font-mono text-[9.5px] uppercase tracking-wider text-muted-foreground font-bold">
            circuit breaker
          </div>
        </div>
        <span
          className={`inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border whitespace-nowrap ${chipClass(breaker.state)}`}
          aria-label={`Circuit breaker state: ${breaker.state}`}
        >
          {breaker.state}
        </span>
      </div>

      <div
        className="grid grid-cols-2 gap-1.5 gap-x-3 mt-auto"
        data-testid={`circuit-card-statgrid-${breaker.serviceName}`}
      >
        <StatItem
          label="Trips"
          value={breaker.tripCount}
          tone={breaker.tripCount > 0 ? 'warn' : 'default'}
        />
        <StatItem
          label="Soglia"
          value={failureThreshold ?? '—'}
          tone={failureThreshold == null ? 'muted' : 'default'}
        />
        <StatItem
          label="Ultima apertura"
          value={breaker.lastTrippedAt ? formatRelative(breaker.lastTrippedAt) : '—'}
          tone={breaker.lastTrippedAt ? 'default' : 'muted'}
        />
        <StatItem
          label="Reset"
          value={breaker.lastResetAt ? formatRelative(breaker.lastResetAt) : '—'}
          tone={breaker.lastResetAt ? 'default' : 'muted'}
        />
      </div>

      {showCooldown && breaker.lastTrippedAt && cooldownSec != null && (
        <CooldownBar trippedAtIso={breaker.lastTrippedAt} cooldownSec={cooldownSec} />
      )}

      {breaker.lastError && (
        <div
          className="font-mono text-[10px] text-rose-700 dark:text-rose-300 truncate"
          title={breaker.lastError}
        >
          err: {breaker.lastError}
        </div>
      )}
    </div>
  );
}

export function CircuitBreakerGrid() {
  const breakersQuery = useCircuitBreakerStates();
  const configQuery = useLlmSystemConfig();

  const breakers = breakersQuery.data ?? [];
  const issueCount = breakers.filter(b => b.state.toLowerCase() !== 'closed').length;
  const cooldownSec = configQuery.data?.circuitBreakerOpenDurationSeconds;
  const failureThreshold = configQuery.data?.circuitBreakerFailureThreshold;

  return (
    <section
      className="rounded-lg border border-border/60 dark:border-zinc-700/60 bg-card/80 dark:bg-zinc-900/80 p-4"
      aria-labelledby="circuit-breakers-heading"
      data-testid="circuit-breaker-grid"
    >
      <header className="flex items-center gap-2 mb-3">
        <h3
          id="circuit-breakers-heading"
          className="font-quicksand text-base font-bold text-foreground"
        >
          Circuit breakers
        </h3>
        <span className="font-mono text-[10.5px] text-muted-foreground">
          {breakersQuery.isLoading ? 'caricamento…' : `${breakers.length} servizi`}
        </span>
        {issueCount > 0 && (
          <span
            className="inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/40"
            role="status"
            aria-label={`${issueCount} circuit breaker${issueCount === 1 ? '' : 's'} require attention`}
          >
            {issueCount} issue
          </span>
        )}
        {cooldownSec != null && failureThreshold != null && (
          <span
            className="ml-auto font-mono text-[10px] text-muted-foreground"
            data-testid="cb-policy-meta"
          >
            policy: {failureThreshold} fail → open · cooldown {cooldownSec}s
          </span>
        )}
      </header>

      {breakersQuery.isError && (
        <div data-testid="circuit-grid-error" className="text-sm text-rose-700 dark:text-rose-300">
          Errore nel caricamento degli stati circuit breaker.
        </div>
      )}

      {breakersQuery.isLoading && (
        <div data-testid="circuit-grid-loading" className="text-sm text-muted-foreground">
          Caricamento…
        </div>
      )}

      {!breakersQuery.isLoading && breakers.length === 0 && (
        <div data-testid="circuit-grid-empty" className="text-sm text-muted-foreground">
          Nessun circuit breaker registrato.
        </div>
      )}

      {breakers.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {breakers.map(b => (
            <CircuitCard
              key={b.serviceName}
              breaker={b}
              cooldownSec={cooldownSec}
              failureThreshold={failureThreshold}
            />
          ))}
        </div>
      )}
    </section>
  );
}
