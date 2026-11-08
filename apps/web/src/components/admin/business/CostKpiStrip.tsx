'use client';

/**
 * CostKpiStrip (#1838 SP5 F4-C5; #4169) — 4 KPI sopra la page Costs.
 *
 *   1. Spesa oggi           (entity-agent) — sparkline 30gg
 *   2. Spesa mese           (entity-event) — dal 1° del mese (UTC)
 *   3. Proiezione fine mese (entity-chat)  — lineare sulla media giornaliera del mese
 *   4. Tetti AI             (entity-toolkit) — il tetto mensile del gateway LLM, con il link al tab
 *                                            Budget dove si cambia
 *
 * #4169 (ADR-095 D6/D8): fino a questa fetta i KPI venivano da `AppBudget`, un secondo budget che il
 * gateway LLM non applicava. Ora la spesa viene dal registro dei costi (la stessa serie del grafico
 * qui sotto, che comprende anche l'infrastruttura), e l'unico budget mostrato è quello vero.
 */

import { useMemo } from 'react';

import Link from 'next/link';

import { useLlmSystemConfig } from '@/hooks/queries/useProviders';
import { useCostBreakdown } from '@/hooks/useCostBreakdown';
import type { BreakdownDay } from '@/lib/api/schemas/business-cost.schemas';
import { cn } from '@/lib/utils';

const SPARK_WIDTH = 70;
const SPARK_HEIGHT = 28;

function buildSparkline(series: readonly number[]): { line: string; fill: string } | null {
  if (series.length < 2) return null;
  const min = Math.min(...series);
  const max = Math.max(...series);
  const range = max - min || 1;
  const stepX = SPARK_WIDTH / (series.length - 1);
  const points = series.map((value, i) => {
    const x = i * stepX;
    const y = SPARK_HEIGHT - ((value - min) / range) * (SPARK_HEIGHT - 4) - 2;
    return `${x.toFixed(2)},${y.toFixed(2)}`;
  });
  const line = `M${points.join(' L')}`;
  const fill = `${line} L${SPARK_WIDTH},${SPARK_HEIGHT} L0,${SPARK_HEIGHT} Z`;
  return { line, fill };
}

function formatUsd(value: number): string {
  return new Intl.NumberFormat('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
}

/** Spesa di oggi e del mese (UTC) e proiezione lineare a fine mese, dai giorni del registro. */
export function summarizeCosts(days: readonly BreakdownDay[], now: Date) {
  const today = now.toISOString().slice(0, 10);
  const month = today.slice(0, 7);
  const spentToday = days
    .filter(d => d.date.slice(0, 10) === today)
    .reduce((sum, d) => sum + d.total, 0);
  const spentMonth = days
    .filter(d => d.date.slice(0, 7) === month)
    .reduce((sum, d) => sum + d.total, 0);
  const dayOfMonth = now.getUTCDate();
  const daysInMonth = new Date(
    Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 0)
  ).getUTCDate();
  const projectedMonthEnd = dayOfMonth > 0 ? (spentMonth / dayOfMonth) * daysInMonth : spentMonth;
  return { spentToday, spentMonth, projectedMonthEnd, daysLeft: daysInMonth - dayOfMonth };
}

interface KpiCardProps {
  label: string;
  value: React.ReactNode;
  trend: React.ReactNode;
  entityBorder: string;
  testId: string;
  ariaLabel: string;
  sparkline?: { line: string; fill: string; colorClass: string } | null;
}

function KpiCard({
  label,
  value,
  trend,
  entityBorder,
  testId,
  ariaLabel,
  sparkline,
}: KpiCardProps) {
  return (
    <article
      data-testid={testId}
      aria-label={ariaLabel}
      className={cn(
        'relative min-h-[88px] rounded-xl border border-border bg-card/60 p-4 border-l-4',
        entityBorder
      )}
    >
      <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
        {label}
      </p>
      <p className="mt-1 font-quicksand text-2xl font-bold text-foreground tabular-nums">{value}</p>
      <p className="mt-1 font-mono text-[11px]">{trend}</p>

      {sparkline && (
        <svg
          aria-hidden
          width={SPARK_WIDTH}
          height={SPARK_HEIGHT}
          viewBox={`0 0 ${SPARK_WIDTH} ${SPARK_HEIGHT}`}
          className={cn('absolute right-3 top-3 opacity-85', sparkline.colorClass)}
        >
          <path d={sparkline.fill} fill="currentColor" opacity={0.14} />
          <path
            d={sparkline.line}
            fill="none"
            stroke="currentColor"
            strokeWidth={1.5}
            strokeLinejoin="round"
          />
        </svg>
      )}
    </article>
  );
}

function KpiSkeleton({ testId }: { testId: string }) {
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

export function CostKpiStrip({ now = new Date() }: { now?: Date } = {}) {
  const breakdownQuery = useCostBreakdown('30d');
  const configQuery = useLlmSystemConfig();

  const days = breakdownQuery.data?.days;
  const summary = useMemo(() => (days ? summarizeCosts(days, now) : null), [days, now]);
  const todaySpark = useMemo(
    () => (days && days.length >= 2 ? buildSparkline(days.map(d => d.total)) : null),
    [days]
  );

  if (breakdownQuery.isLoading) {
    return (
      <div
        data-testid="cost-kpi-strip"
        className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4"
        aria-busy="true"
      >
        <KpiSkeleton testId="cost-kpi-skeleton-today" />
        <KpiSkeleton testId="cost-kpi-skeleton-month" />
        <KpiSkeleton testId="cost-kpi-skeleton-projected" />
        <KpiSkeleton testId="cost-kpi-skeleton-caps" />
      </div>
    );
  }

  const monthlyCap = configQuery.data?.monthlyBudgetUsd;

  return (
    <div
      data-testid="cost-kpi-strip"
      className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4"
    >
      {summary ? (
        <>
          <KpiCard
            testId="cost-kpi-today"
            label="Spesa oggi"
            ariaLabel={`Spesa oggi USD ${formatUsd(summary.spentToday)}`}
            value={
              <>
                <span className="text-base font-semibold text-muted-foreground">$</span>
                {formatUsd(summary.spentToday)}
              </>
            }
            trend={<span className="text-muted-foreground">▬ registro costi, ultimi 30gg</span>}
            entityBorder="border-l-entity-agent"
            sparkline={
              todaySpark
                ? { line: todaySpark.line, fill: todaySpark.fill, colorClass: 'text-entity-agent' }
                : null
            }
          />
          <KpiCard
            testId="cost-kpi-month"
            label="Spesa mese"
            ariaLabel={`Spesa mese USD ${formatUsd(summary.spentMonth)}`}
            value={
              <>
                <span className="text-base font-semibold text-muted-foreground">$</span>
                {formatUsd(summary.spentMonth)}
              </>
            }
            trend={
              <span className="text-muted-foreground">
                ▬ dal 1° del mese · {summary.daysLeft}gg residui
              </span>
            }
            entityBorder="border-l-entity-event"
          />
          <KpiCard
            testId="cost-kpi-projected"
            label="Proiezione fine mese"
            ariaLabel={`Proiezione fine mese USD ${formatUsd(summary.projectedMonthEnd)}`}
            value={
              <>
                <span className="text-base font-semibold text-muted-foreground">$</span>
                {formatUsd(summary.projectedMonthEnd)}
              </>
            }
            trend={<span className="text-muted-foreground">▬ lineare sulla media del mese</span>}
            entityBorder="border-l-entity-chat"
          />
        </>
      ) : (
        <article
          data-testid="cost-kpi-unavailable"
          className="rounded-xl border border-dashed border-border bg-card/40 p-4 text-sm text-muted-foreground sm:col-span-2 lg:col-span-3"
        >
          Spesa non disponibile: il registro dei costi non ha risposto.
        </article>
      )}
      <article
        data-testid="cost-kpi-caps"
        aria-label="Tetti di spesa dell'AI"
        className="relative min-h-[88px] rounded-xl border border-border bg-card/60 p-4 border-l-4 border-l-entity-toolkit"
      >
        <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
          Tetti AI
        </p>
        <p className="mt-1 font-quicksand text-2xl font-bold text-foreground tabular-nums">
          {monthlyCap != null ? (
            <>
              <span className="text-base font-semibold text-muted-foreground">$</span>
              {formatUsd(monthlyCap)}
              <span className="text-sm font-semibold text-muted-foreground"> / mese</span>
            </>
          ) : (
            '—'
          )}
        </p>
        <p className="mt-1 font-mono text-[11px] text-muted-foreground">
          Il gateway li applica prima di ogni chiamata.{' '}
          <Link
            href="/admin/ai?tab=config"
            className="underline underline-offset-2 text-foreground"
          >
            Gestisci in AI → Budget
          </Link>
        </p>
      </article>
    </div>
  );
}
