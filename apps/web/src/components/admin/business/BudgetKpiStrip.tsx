'use client';

/**
 * #1838 SP5 F4-C5 — BudgetKpiStrip
 *
 * KPI strip per `/admin/business` (mockup `sp5-admin-budget.html` §1, kpi-strip).
 * 4 metriche: spesa oggi, spesa mese, budget residuo, proiezione fine mese.
 *
 * **BE pending**: il backend non espone ancora un endpoint cost aggregation
 * (`/admin/business/breakdown`, `/admin/budget`). UI mostra valori placeholder
 * "—" con tooltip esplicativo finché i BE endpoint non saranno disponibili.
 */

interface KpiBoxProps {
  readonly label: string;
  readonly value: string;
  readonly trend?: string;
  readonly tone: 'agent' | 'event' | 'toolkit' | 'chat';
  readonly tooltip?: string;
}

function KpiBox({ label, value, trend, tone, tooltip }: KpiBoxProps) {
  const toneClass: Record<KpiBoxProps['tone'], string> = {
    agent: 'border-amber-500/30 bg-amber-500/5',
    event: 'border-rose-500/30 bg-rose-500/5',
    toolkit: 'border-teal-500/30 bg-teal-500/5',
    chat: 'border-cyan-500/30 bg-cyan-500/5',
  };

  return (
    <div
      className={`rounded-lg border ${toneClass[tone]} p-4`}
      title={tooltip}
      data-testid={`budget-kpi-${label.toLowerCase().replace(/\s+/g, '-')}`}
    >
      <div className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
        {label}
      </div>
      <div className="mt-1 font-quicksand text-2xl font-bold text-foreground">{value}</div>
      {trend && <div className="mt-1 font-mono text-[10px] text-muted-foreground">{trend}</div>}
    </div>
  );
}

export function BudgetKpiStrip() {
  return (
    <section
      className="grid grid-cols-2 gap-3 sm:grid-cols-4"
      aria-label="Budget KPI"
      data-testid="budget-kpi-strip"
    >
      <KpiBox
        label="Spesa oggi"
        value="—"
        trend="aggregato BE pending"
        tone="agent"
        tooltip="Endpoint /admin/business/breakdown?range=24h non ancora implementato"
      />
      <KpiBox
        label="Spesa mese"
        value="—"
        trend="aggregato BE pending"
        tone="event"
        tooltip="Endpoint /admin/business/breakdown?range=30d non ancora implementato"
      />
      <KpiBox
        label="Budget residuo"
        value="—"
        trend="config /admin/budget pending"
        tone="toolkit"
        tooltip="Endpoint /admin/budget non ancora implementato"
      />
      <KpiBox
        label="Proiezione fine mese"
        value="—"
        trend="ETA exhaust pending"
        tone="chat"
        tooltip="Calcolo proiezione richiede serie storica + budget config"
      />
    </section>
  );
}
