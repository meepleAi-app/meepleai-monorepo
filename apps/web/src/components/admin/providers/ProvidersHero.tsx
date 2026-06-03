'use client';

/**
 * #1834 SP5 F4-C3 — ProvidersHero
 *
 * KPI strip per `/admin/providers` (mockup `sp5-admin-providers.html` §1, hero panel).
 *
 * **PR1 reduction (sessione 2026-06-03)**: ridotto da 4 KPI a 2 KPI reali.
 * I 3 KPI cross-provider (p95 latency, error rate 24h, cost 24h) richiedono
 * un endpoint aggregato BE che non esiste ancora — issue tracker FE-only.
 *
 * Quando il BE arriverà (issue follow-up), riaggiungere i 3 KPI con valori reali.
 */

import { useMemo } from 'react';

import { useCircuitBreakerStates } from '@/hooks/queries/useProviders';

interface KpiBoxProps {
  readonly label: string;
  readonly value: string;
  readonly trend?: string;
  readonly tone: 'tool' | 'event';
  readonly tooltip?: string;
}

function KpiBox({ label, value, trend, tone, tooltip }: KpiBoxProps) {
  const toneClass: Record<KpiBoxProps['tone'], string> = {
    tool: 'border-entity-tool/30 bg-entity-tool/5',
    event: 'border-entity-event/30 bg-entity-event/5',
  };

  return (
    <div
      className={`rounded-lg border ${toneClass[tone]} p-4`}
      title={tooltip}
      data-testid={`providers-kpi-${label.toLowerCase().replace(/\s+/g, '-')}`}
    >
      <div className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
        {label}
      </div>
      <div className="mt-1 font-quicksand text-2xl font-bold text-foreground">{value}</div>
      {trend && <div className="mt-1 font-mono text-[10px] text-muted-foreground">{trend}</div>}
    </div>
  );
}

export function ProvidersHero() {
  const circuitBreakersQuery = useCircuitBreakerStates();

  const stats = useMemo(() => {
    const breakers = circuitBreakersQuery.data ?? [];
    const open = breakers.filter(b => b.state.toLowerCase() === 'open').length;
    const halfOpen = breakers.filter(b => {
      const s = b.state.toLowerCase();
      return s === 'half-open' || s === 'halfopen';
    }).length;
    const closed = breakers.filter(b => b.state.toLowerCase() === 'closed').length;
    return { total: breakers.length, open, halfOpen, closed };
  }, [circuitBreakersQuery.data]);

  const trippedCount = stats.open + stats.halfOpen;
  const healthValue = stats.total === 0 ? '—' : `${stats.closed}/${stats.total}`;
  const healthTrend =
    stats.total === 0
      ? 'nessun servizio'
      : trippedCount === 0
        ? 'tutti closed'
        : `${stats.open} open · ${stats.halfOpen} half-open`;

  return (
    <section
      className="grid grid-cols-1 gap-3 sm:grid-cols-2"
      aria-label="Provider KPI"
      data-testid="providers-hero"
    >
      <KpiBox
        label="Servizi monitorati"
        value={circuitBreakersQuery.isLoading ? '…' : String(stats.total)}
        trend={
          stats.total === 0 && !circuitBreakersQuery.isLoading
            ? 'nessun circuit registrato'
            : 'circuit breakers attivi'
        }
        tone="tool"
        tooltip="Numero di servizi protetti da Polly circuit breaker"
      />
      <KpiBox
        label="Circuit health"
        value={circuitBreakersQuery.isLoading ? '…' : healthValue}
        trend={healthTrend}
        tone="event"
        tooltip="Servizi con circuit closed (healthy) su totale"
      />
    </section>
  );
}
