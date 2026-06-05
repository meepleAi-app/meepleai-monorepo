'use client';

/**
 * AlertKpiStrip (#1840 SP5 F4-C7) — 3 KPI sopra AlertRuleList.
 *
 * Mockup `sp5-admin-alerts.html` riga 246-276:
 *   1. Regole attive   (active/total + breakdown severity)
 *   2. Alert oggi      (today + resolved + trend)
 *   3. Canali config.  (total + per-type breakdown)
 *
 * Data via {@link useAlertKpis} (3 React Query in parallelo con shared keys
 * verso AlertRuleList / AlertHistoryTab / CanaliDrawer).
 *
 * Sparkline omessa: gli alert non hanno time-series storica naturale (history è
 * event-based, non sampled). Pattern coerente con KPISparklineStrip C1 dove i
 * KPI senza time-series rendono senza svg.
 */

import { useAlertKpis } from '@/hooks/useAlertKpis';
import { cn } from '@/lib/utils';

function KpiCardSkeleton({ testId }: { testId: string }) {
  return (
    <div
      data-testid={testId}
      className="relative min-h-[88px] animate-pulse rounded-xl border border-border bg-card/60 p-4"
    >
      <div className="mb-2 h-3 w-24 rounded bg-muted" />
      <div className="h-7 w-16 rounded bg-muted" />
    </div>
  );
}

interface KpiCardProps {
  label: string;
  value: React.ReactNode;
  trend: React.ReactNode;
  entityBorderClass: string;
  testId: string;
  ariaLabel: string;
}

function KpiCard({ label, value, trend, entityBorderClass, testId, ariaLabel }: KpiCardProps) {
  return (
    <article
      data-testid={testId}
      aria-label={ariaLabel}
      className={cn(
        'relative min-h-[88px] rounded-xl border border-border bg-card/60 p-4 border-l-4',
        entityBorderClass
      )}
    >
      <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
        {label}
      </p>
      <p className="mt-1 font-quicksand text-2xl font-bold text-foreground tabular-nums">{value}</p>
      <p className="mt-1 font-mono text-[11px]">{trend}</p>
    </article>
  );
}

export function AlertKpiStrip() {
  const { kpis, isLoading } = useAlertKpis();

  if (isLoading) {
    return (
      <div
        data-testid="alert-kpi-strip"
        className="grid grid-cols-1 gap-3 sm:grid-cols-3"
        aria-busy="true"
      >
        <KpiCardSkeleton testId="alert-kpi-skeleton-rules" />
        <KpiCardSkeleton testId="alert-kpi-skeleton-today" />
        <KpiCardSkeleton testId="alert-kpi-skeleton-channels" />
      </div>
    );
  }

  const severityBreakdown = [
    kpis.rulesBySeverity.critical ? `${kpis.rulesBySeverity.critical} critical` : null,
    kpis.rulesBySeverity.error ? `${kpis.rulesBySeverity.error} error` : null,
    kpis.rulesBySeverity.warning ? `${kpis.rulesBySeverity.warning} warning` : null,
    kpis.rulesBySeverity.info ? `${kpis.rulesBySeverity.info} info` : null,
    kpis.rulesDisabled ? `${kpis.rulesDisabled} disattiv.` : null,
  ]
    .filter((v): v is string => Boolean(v))
    .join(' · ');

  const channelsBreakdown = [
    kpis.channelsByType.slack ? `${kpis.channelsByType.slack} slack` : null,
    kpis.channelsByType.email ? `${kpis.channelsByType.email} email` : null,
  ]
    .filter((v): v is string => Boolean(v))
    .join(' · ');

  return (
    <div data-testid="alert-kpi-strip" className="grid grid-cols-1 gap-3 sm:grid-cols-3">
      <KpiCard
        testId="alert-kpi-rules"
        label="Regole attive"
        ariaLabel={`Regole attive ${kpis.rulesActive} su ${kpis.rulesTotal}, ${kpis.rulesDisabled} disattivate`}
        value={
          <>
            {kpis.rulesActive}
            <span className="text-sm font-semibold text-muted-foreground">/{kpis.rulesTotal}</span>
          </>
        }
        trend={
          <span className="text-muted-foreground">
            {severityBreakdown ? `▬ ${severityBreakdown}` : '▬ nessuna regola'}
          </span>
        }
        entityBorderClass="border-l-entity-toolkit"
      />

      <KpiCard
        testId="alert-kpi-today"
        label="Alert oggi"
        ariaLabel={`Alert oggi ${kpis.alertsToday}, ${kpis.alertsResolvedToday} risolti, ${kpis.alertsActive} attivi totali`}
        value={
          <>
            {kpis.alertsToday}
            <span className="text-sm font-semibold text-muted-foreground">
              {kpis.alertsResolvedToday > 0 ? ` · ${kpis.alertsResolvedToday} risolti` : ''}
            </span>
          </>
        }
        trend={
          kpis.alertsActive > 0 ? (
            <span className="text-rose-600 dark:text-rose-400">
              ▲ {kpis.alertsActive} attivo{kpis.alertsActive === 1 ? '' : 'i'} totale
              {kpis.alertsActive === 1 ? '' : 'i'}
            </span>
          ) : (
            <span className="text-muted-foreground">▬ nessun alert attivo</span>
          )
        }
        entityBorderClass="border-l-entity-event"
      />

      <KpiCard
        testId="alert-kpi-channels"
        label="Canali configurati"
        ariaLabel={`Canali configurati ${kpis.channelsTotal}, ${channelsBreakdown || 'nessun canale'}`}
        value={kpis.channelsTotal}
        trend={
          <span className="text-muted-foreground">
            {channelsBreakdown ? `▬ ${channelsBreakdown}` : '▬ nessun canale'}
          </span>
        }
        entityBorderClass="border-l-entity-chat"
      />
    </div>
  );
}
