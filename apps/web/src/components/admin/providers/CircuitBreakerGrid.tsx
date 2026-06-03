/* eslint-disable local/no-hardcoded-color-utility -- admin circuit breaker grid: emerald/amber/rose status palette + zinc default fallback (admin convention DS-13c, scope deferred to DS-16) */
'use client';

/**
 * #1834 SP5 F4-C3 — CircuitBreakerGrid
 *
 * Grid degli stati Polly circuit breaker per servizi LLM
 * (mockup `sp5-admin-providers.html` cluster sotto la routing chain).
 *
 * Sostituisce il pattern client-direct di `monitor/services/CircuitBreakerPanel.tsx`
 * (vecchio) con uno SP5-conformant che usa `useCircuitBreakerStates` (hook condiviso).
 *
 * Stato visivo:
 * - `Closed` → emerald (verde) — flow normale
 * - `Open` → rose (rosso) — circuit aperto, last failure visibile
 * - `HalfOpen` → amber (giallo) — recupero in corso
 */

import { useCircuitBreakerStates } from '@/hooks/queries/useProviders';
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

function CircuitCard({ breaker }: { readonly breaker: CircuitBreakerState }) {
  return (
    <div
      data-testid={`circuit-card-${breaker.serviceName}`}
      className={`rounded-lg border p-3 ${stateClass(breaker.state)}`}
    >
      <div className="flex items-center justify-between gap-2">
        <span
          className="font-quicksand text-sm font-bold text-foreground truncate"
          title={breaker.serviceName}
        >
          {breaker.serviceName}
        </span>
        <span
          className={`inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border ${chipClass(breaker.state)}`}
        >
          {breaker.state}
        </span>
      </div>
      <dl className="mt-2 font-mono text-[10.5px] text-muted-foreground space-y-0.5">
        <div>
          <dt className="inline">Trips: </dt>
          <dd className="inline text-foreground">{breaker.tripCount}</dd>
        </div>
        {breaker.lastTrippedAt && (
          <div>
            <dt className="inline">Last trip: </dt>
            <dd className="inline">{new Date(breaker.lastTrippedAt).toLocaleString('it-IT')}</dd>
          </div>
        )}
        {breaker.lastResetAt && (
          <div>
            <dt className="inline">Last reset: </dt>
            <dd className="inline">{new Date(breaker.lastResetAt).toLocaleString('it-IT')}</dd>
          </div>
        )}
        {breaker.lastError && (
          <div className="text-rose-700 dark:text-rose-300 truncate" title={breaker.lastError}>
            <dt className="inline">Err: </dt>
            <dd className="inline">{breaker.lastError}</dd>
          </div>
        )}
      </dl>
    </div>
  );
}

export function CircuitBreakerGrid() {
  const breakersQuery = useCircuitBreakerStates();

  const breakers = breakersQuery.data ?? [];
  const issueCount = breakers.filter(b => b.state.toLowerCase() !== 'closed').length;

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
          <span className="inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/40">
            {issueCount} issue
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
            <CircuitCard key={b.serviceName} breaker={b} />
          ))}
        </div>
      )}
    </section>
  );
}
