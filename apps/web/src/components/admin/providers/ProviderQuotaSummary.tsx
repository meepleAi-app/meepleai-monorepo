'use client';

/**
 * Issue #3043 — ProviderQuotaSummary
 *
 * Widget riepilogo crediti aggregato per `/admin/providers`: mostra remaining/used/limit
 * per ciascun provider quota-capable (openrouter, deepseek) con UN SOLO fetch aggregato
 * (`useProvidersQuota`), eliminando l'N+1 per-riga della tabella. `ollama-local` è escluso
 * (nessuna quota API → non è in SupportedProviderNames). Itera l'array di risposta, MAI
 * `KNOWN_PROVIDERS`. Solo token semantici + entity utilities (AA-safe, no colori hardcoded).
 */

import { useMemo } from 'react';

import { useProvidersQuota } from '@/hooks/queries/useProviders';
import type { ProviderQuota } from '@/lib/api/schemas/providers';

function formatUsd(v: number | null): string {
  return v === null ? '—' : `$${v.toFixed(2)}`;
}

function quotaStatus(q: ProviderQuota): { label: string; showNumbers: boolean } {
  if (!q.tokenConfigured) return { label: 'no token', showNumbers: false };
  if (!q.quotaSupported) return { label: 'n/d', showNumbers: false };
  if (q.errorCode) return { label: 'degraded', showNumbers: false };
  return { label: 'ok', showNumbers: true };
}

function QuotaCard({ quota }: { quota: ProviderQuota }) {
  const { label, showNumbers } = quotaStatus(quota);
  return (
    <div
      className="rounded-lg border border-entity-tool/30 bg-entity-tool/5 p-4"
      data-testid={`quota-card-${quota.providerName}`}
      aria-label={`Credito ${quota.providerName}: ${label}`}
    >
      <div className="flex items-center justify-between">
        <div className="font-quicksand text-sm font-bold text-foreground">{quota.providerName}</div>
        <span className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
          {label}
        </span>
      </div>
      <div className="mt-2 font-quicksand text-2xl font-bold text-foreground tabular-nums">
        {showNumbers ? formatUsd(quota.remainingUsd) : '—'}
      </div>
      <div className="mt-1 font-mono text-[10px] text-muted-foreground">
        rimanente · usato {showNumbers ? formatUsd(quota.usedUsd) : '—'} /{' '}
        {showNumbers ? formatUsd(quota.limitUsd) : '—'}
      </div>
    </div>
  );
}

function isCountable(q: ProviderQuota): boolean {
  return q.quotaSupported && q.tokenConfigured && q.errorCode === null && q.remainingUsd !== null;
}

export function ProviderQuotaSummary() {
  const query = useProvidersQuota();

  const list = useMemo(() => query.data ?? [], [query.data]);
  const total = useMemo(
    () => list.filter(isCountable).reduce((sum, q) => sum + (q.remainingUsd ?? 0), 0),
    [list]
  );
  const hasNumbers = list.some(isCountable);

  if (query.isLoading) {
    return (
      <section aria-label="Crediti provider" data-testid="provider-quota-summary">
        <div
          data-testid="provider-quota-summary-loading"
          className="rounded-lg border border-border bg-card/60 p-4 font-mono text-[11px] text-muted-foreground"
        >
          Caricamento crediti…
        </div>
      </section>
    );
  }

  if (query.isError) {
    return (
      <section aria-label="Crediti provider" data-testid="provider-quota-summary">
        <div
          role="alert"
          data-testid="provider-quota-summary-error"
          className="rounded-lg border border-border bg-card/60 p-4 font-mono text-[11px] text-muted-foreground"
        >
          Impossibile caricare i crediti dei provider.
        </div>
      </section>
    );
  }

  return (
    <section
      aria-label="Crediti provider"
      data-testid="provider-quota-summary"
      className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3"
    >
      {list.map(q => (
        <QuotaCard key={q.providerName} quota={q} />
      ))}
      <div
        className="rounded-lg border border-entity-event/30 bg-entity-event/5 p-4"
        data-testid="provider-quota-summary-total"
        aria-label={`Totale credito rimanente: ${hasNumbers ? formatUsd(total) : 'non disponibile'}`}
      >
        <div className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
          Totale rimanente
        </div>
        <div className="mt-1 font-quicksand text-2xl font-bold text-foreground tabular-nums">
          {hasNumbers ? formatUsd(total) : '—'}
        </div>
        <div className="mt-1 font-mono text-[10px] text-muted-foreground">
          {list.length} provider con quota
        </div>
      </div>
    </section>
  );
}
