'use client';

/**
 * Issue #3043 — ProviderQuotaSummary
 *
 * Widget riepilogo crediti su `/admin/providers`: mostra i NUMERI di credito
 * (remaining/used/limit) per ciascun provider quota-capable (openrouter, deepseek) — numeri
 * che la `ProviderTable` NON mostra (solo un chip di stato) — in un colpo d'occhio con un
 * fetch aggregato (`useProvidersQuota`). La cache server (5min) è condivisa con la tabella
 * per-provider, quindi non c'è fetch upstream aggiuntivo (il widget aggiunge una sola
 * richiesta client verso l'endpoint plurale, servita dalla stessa cache). `ollama-local` è
 * escluso (nessuna quota API). Itera l'array di risposta, MAI `KNOWN_PROVIDERS`. Solo entity
 * utilities + token semantici (AA-safe, no colori hardcoded).
 */

import { useProvidersQuota } from '@/hooks/queries/useProviders';
import type { ProviderQuota } from '@/lib/api/schemas/providers';

function formatUsd(v: number | null): string {
  return v === null ? '—' : `$${v.toFixed(2)}`;
}

/** Un provider contribuisce al totale solo se ha un remaining numerico reale. */
function isCountable(q: ProviderQuota): boolean {
  return q.quotaSupported && q.tokenConfigured && q.errorCode === null && q.remainingUsd !== null;
}

/**
 * Etichetta di stato. 'ok' SOLO quando i numeri sono mostrabili (allineato a isCountable):
 * un provider healthy ma senza limite di spesa (pay-as-you-go → remainingUsd null) è
 * etichettato 'nessun limite', non 'ok', così la card non mostra '—' accanto a uno stato ok.
 */
function quotaLabel(q: ProviderQuota): string {
  if (!q.tokenConfigured) return 'no token';
  if (!q.quotaSupported) return 'n/d';
  if (q.errorCode) return 'degraded';
  if (q.remainingUsd === null) return 'nessun limite';
  return 'ok';
}

function QuotaCard({ quota }: { quota: ProviderQuota }) {
  const label = quotaLabel(quota);
  const showNumbers = isCountable(quota);
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

export function ProviderQuotaSummary() {
  const query = useProvidersQuota();

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

  const list = query.data ?? [];
  const countable = list.filter(isCountable);
  const total = countable.reduce((sum, q) => sum + (q.remainingUsd ?? 0), 0);
  const hasNumbers = countable.length > 0;

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
          {countable.length} di {list.length} provider con credito
        </div>
      </div>
    </section>
  );
}
