'use client';

/**
 * BudgetKpiStrip (#1838 SP5 F4-C5) — 4 KPI sopra la page Budget & Cost.
 *
 * Mockup `sp5-admin-budget.html` riga 263-300:
 *   1. Spesa oggi          (entity-agent) — sparkline 30gg + trend vs ieri
 *   2. Spesa mese          (entity-event) — progress bar spent/limit
 *   3. Budget residuo      (entity-toolkit) — days remaining + daily capacity
 *   4. Proiezione fine mese (entity-chat) — delta vs budget
 *
 * Data via {@link useBudget} (1 query, 30s refetch). Sparkline `Spesa oggi`
 * deriva dai last 30gg di `useCostBreakdown('30d').days` per coerenza.
 *
 * Empty state: quando `budget === null` (mai configurato) mostra placeholder
 * "—" + tooltip CTA "Imposta budget".
 */

import { useMemo } from 'react';

import { useBudget } from '@/hooks/useBudget';
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

interface KpiCardProps {
  label: string;
  value: React.ReactNode;
  trend: React.ReactNode;
  entityBorder: string;
  testId: string;
  ariaLabel: string;
  sparkline?: { line: string; fill: string; colorClass: string } | null;
  progressPct?: number | null;
}

function KpiCard({
  label,
  value,
  trend,
  entityBorder,
  testId,
  ariaLabel,
  sparkline,
  progressPct,
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

      {progressPct != null && (
        <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-muted">
          <div
            className="h-full rounded-full bg-rose-500/70 dark:bg-rose-400/70 transition-all"
            style={{ width: `${Math.min(progressPct, 100)}%` }}
            aria-hidden
          />
        </div>
      )}

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

function formatUsd(value: number): string {
  return new Intl.NumberFormat('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
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

export function BudgetKpiStrip() {
  const { budget, isLoading } = useBudget();
  const breakdownQuery = useCostBreakdown('30d');

  // Sparkline "Spesa oggi" trend: last 30 daily totals from CostStackedArea data.
  const todaySpark = useMemo(() => {
    const days: BreakdownDay[] | undefined = breakdownQuery.data?.days;
    if (!days || days.length < 2) return null;
    const series = days.map(d => d.total);
    return buildSparkline(series);
  }, [breakdownQuery.data]);

  if (isLoading) {
    return (
      <div
        data-testid="budget-kpi-strip"
        className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4"
        aria-busy="true"
      >
        <KpiSkeleton testId="budget-kpi-skeleton-today" />
        <KpiSkeleton testId="budget-kpi-skeleton-month" />
        <KpiSkeleton testId="budget-kpi-skeleton-remaining" />
        <KpiSkeleton testId="budget-kpi-skeleton-projected" />
      </div>
    );
  }

  // Empty state: budget never configured → show placeholders with CTA tooltip.
  if (!budget) {
    return (
      <div
        data-testid="budget-kpi-strip"
        className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4"
        aria-label="Budget KPI · non configurato"
      >
        {(['today', 'month', 'remaining', 'projected'] as const).map(slot => (
          <article
            key={slot}
            data-testid={`budget-kpi-empty-${slot}`}
            className="min-h-[88px] rounded-xl border border-dashed border-border bg-card/40 p-4"
            title="Clicca '+ Imposta budget' nell'header per configurare il budget mensile."
          >
            <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
              {slot === 'today'
                ? 'Spesa oggi'
                : slot === 'month'
                  ? 'Spesa mese'
                  : slot === 'remaining'
                    ? 'Budget residuo'
                    : 'Proiezione fine mese'}
            </p>
            <p className="mt-1 font-quicksand text-2xl font-bold text-muted-foreground tabular-nums">
              —
            </p>
            <p className="mt-1 font-mono text-[11px] text-muted-foreground">
              Imposta budget per vedere KPI
            </p>
          </article>
        ))}
      </div>
    );
  }

  const { monthlyLimit, currency, spent, daysRemaining } = budget;
  const remaining = Math.max(monthlyLimit - spent.thisMonth, 0);
  const monthPct = monthlyLimit > 0 ? (spent.thisMonth / monthlyLimit) * 100 : 0;
  const projectionDeltaPct = monthlyLimit > 0 ? (spent.projectedMonthEnd / monthlyLimit) * 100 : 0;
  const dailyCapacity = daysRemaining > 0 ? remaining / daysRemaining : 0;
  const projectionUnder = spent.projectedMonthEnd <= monthlyLimit;
  const projectionDelta = monthlyLimit - spent.projectedMonthEnd;

  return (
    <div
      data-testid="budget-kpi-strip"
      className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4"
    >
      <KpiCard
        testId="budget-kpi-today"
        label="Spesa oggi"
        ariaLabel={`Spesa oggi ${currency} ${formatUsd(spent.today)}`}
        value={
          <>
            <span className="text-base font-semibold text-muted-foreground">$</span>
            {formatUsd(spent.today)}
          </>
        }
        trend={<span className="text-muted-foreground">▬ aggregato 30gg disponibile sotto</span>}
        entityBorder="border-l-entity-agent"
        sparkline={
          todaySpark
            ? {
                line: todaySpark.line,
                fill: todaySpark.fill,
                colorClass: 'text-entity-agent',
              }
            : null
        }
      />

      <KpiCard
        testId="budget-kpi-month"
        label="Spesa mese"
        ariaLabel={`Spesa mese ${currency} ${formatUsd(spent.thisMonth)} su ${formatUsd(monthlyLimit)}`}
        value={
          <>
            <span className="text-base font-semibold text-muted-foreground">$</span>
            {formatUsd(spent.thisMonth)}
            <span className="text-sm font-semibold text-muted-foreground">
              {' '}
              / {formatUsd(monthlyLimit)}
            </span>
          </>
        }
        trend={
          <span className="text-muted-foreground">
            ▬ {monthPct.toFixed(1)}% del budget · {daysRemaining}gg residui
          </span>
        }
        entityBorder="border-l-entity-event"
        progressPct={monthPct}
      />

      <KpiCard
        testId="budget-kpi-remaining"
        label="Budget residuo"
        ariaLabel={`Budget residuo ${currency} ${formatUsd(remaining)}`}
        value={
          <>
            <span className="text-base font-semibold text-muted-foreground">$</span>
            {formatUsd(remaining)}
          </>
        }
        trend={
          <span className="text-emerald-600 dark:text-emerald-400">
            ▲ {daysRemaining}gg residui · ${formatUsd(dailyCapacity)}/d capacità
          </span>
        }
        entityBorder="border-l-entity-toolkit"
      />

      <KpiCard
        testId="budget-kpi-projected"
        label="Proiezione fine mese"
        ariaLabel={`Proiezione fine mese ${currency} ${formatUsd(spent.projectedMonthEnd)}`}
        value={
          <>
            <span className="text-base font-semibold text-muted-foreground">$</span>
            {formatUsd(spent.projectedMonthEnd)}
          </>
        }
        trend={
          projectionUnder ? (
            <span className="text-emerald-600 dark:text-emerald-400">
              ▼ -${formatUsd(projectionDelta)} sotto budget · {projectionDeltaPct.toFixed(1)}%
            </span>
          ) : (
            <span className="text-rose-600 dark:text-rose-400">
              ▲ +${formatUsd(-projectionDelta)} sopra budget · {projectionDeltaPct.toFixed(1)}%
            </span>
          )
        }
        entityBorder="border-l-entity-chat"
      />
    </div>
  );
}
