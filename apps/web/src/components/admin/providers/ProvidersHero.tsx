'use client';

/**
 * #1834 SP5 F4-C3 — ProvidersHero
 *
 * KPI strip per `/admin/providers` (mockup `sp5-admin-providers.html` §1, hero panel).
 * 4 metriche: requests/h, p95 latency, error rate 24h, cost 24h.
 *
 * **BE pending**: il backend non espone ancora un endpoint di aggregato cross-provider.
 * Per ora i valori sono derivati lato FE da `useCircuitBreakerStates` quando possibile,
 * altrimenti mostrano `—` con tooltip esplicativo. Issue follow-up: aggregato BE.
 */

import { useMemo } from 'react';

import { useCircuitBreakerStates } from '@/hooks/queries/useProviders';

interface KpiBoxProps {
  readonly label: string;
  readonly value: string;
  readonly trend?: string;
  readonly tone: 'agent' | 'tool' | 'event' | 'kb';
  readonly tooltip?: string;
}

function KpiBox({ label, value, trend, tone, tooltip }: KpiBoxProps) {
  const toneClass: Record<KpiBoxProps['tone'], string> = {
    agent: 'border-amber-500/30 bg-amber-500/5',
    tool: 'border-cyan-500/30 bg-cyan-500/5',
    event: 'border-rose-500/30 bg-rose-500/5',
    kb: 'border-teal-500/30 bg-teal-500/5',
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
    const trippedCount = breakers.filter(
      b => b.state.toLowerCase() === 'open' || b.state.toLowerCase() === 'half-open'
    ).length;
    return { totalServices: breakers.length, trippedCount };
  }, [circuitBreakersQuery.data]);

  return (
    <section
      className="grid grid-cols-2 gap-3 sm:grid-cols-4"
      aria-label="Provider KPI"
      data-testid="providers-hero"
    >
      <KpiBox
        label="Servizi monitorati"
        value={circuitBreakersQuery.isLoading ? '…' : String(stats.totalServices)}
        trend={stats.trippedCount > 0 ? `${stats.trippedCount} circuit open` : 'tutti closed'}
        tone="tool"
        tooltip="Numero di servizi protetti da Polly circuit breaker"
      />
      <KpiBox
        label="Latency p95"
        value="—"
        trend="aggregato BE pending"
        tone="event"
        tooltip="Metrica aggregata cross-provider non ancora esposta dal backend"
      />
      <KpiBox
        label="Error rate 24h"
        value="—"
        trend="aggregato BE pending"
        tone="agent"
        tooltip="Metrica aggregata cross-provider non ancora esposta dal backend"
      />
      <KpiBox
        label="Costo 24h"
        value="—"
        trend="aggregato BE pending"
        tone="kb"
        tooltip="Metrica aggregata cross-provider non ancora esposta dal backend"
      />
    </section>
  );
}
