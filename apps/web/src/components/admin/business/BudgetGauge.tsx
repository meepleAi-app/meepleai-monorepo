'use client';

/**
 * BudgetGauge (#1838 SP5 F4-C5) — radial gauge SVG spent vs monthly limit.
 *
 * Mockup `sp5-admin-budget.html` riga 575-619 (Scenario F nello spec doc).
 *
 * Renders a 180° arc with conditional color (green <alert%, amber <critical%,
 * rose ≥critical%). The threshold percentages live on the AppBudget aggregate
 * so the admin can tune them per environment via SetBudgetDialog.
 *
 * ETA exhaust is a linear projection: `daysRemaining * (spent / dayOfMonth)`.
 */

import { useMemo } from 'react';

import { useBudget } from '@/hooks/useBudget';
import { cn } from '@/lib/utils';

const ARC_RADIUS = 90;
const ARC_CENTER = 100;
const ARC_STROKE = 14;
// 180° arc from -180° (left) to 0° (right). Total arc length = pi * R.
const ARC_CIRCUMFERENCE = Math.PI * ARC_RADIUS;

function arcPath(): string {
  // Half-circle path going clockwise from left edge to right edge.
  return `M ${ARC_CENTER - ARC_RADIUS} ${ARC_CENTER} A ${ARC_RADIUS} ${ARC_RADIUS} 0 0 1 ${ARC_CENTER + ARC_RADIUS} ${ARC_CENTER}`;
}

function formatCurrency(value: number): string {
  return `$${value.toFixed(2)}`;
}

function etaExhaustLabel(
  spentThisMonth: number,
  monthlyLimit: number,
  daysRemaining: number
): string {
  if (spentThisMonth <= 0 || daysRemaining <= 0) return 'ETA: —';
  const today = new Date();
  const dayOfMonth = today.getDate();
  if (dayOfMonth <= 0) return 'ETA: —';
  const dailyAvg = spentThisMonth / dayOfMonth;
  if (dailyAvg <= 0) return 'ETA: fine mese';
  const daysToExhaust = (monthlyLimit - spentThisMonth) / dailyAvg;
  if (daysToExhaust >= daysRemaining) {
    return 'ETA: oltre fine mese';
  }
  const exhaustDate = new Date(today);
  exhaustDate.setDate(today.getDate() + Math.floor(daysToExhaust));
  const fmt = exhaustDate.toLocaleDateString('it-IT', { day: '2-digit', month: '2-digit' });
  return `ETA exhaust: ${fmt}`;
}

interface BudgetGaugeProps {
  className?: string;
}

export function BudgetGauge({ className }: BudgetGaugeProps) {
  const { budget, isLoading } = useBudget();

  const view = useMemo(() => {
    if (!budget) return null;
    const { monthlyLimit, spent, alertThresholdPct, criticalThresholdPct, daysRemaining } = budget;
    const pct = monthlyLimit > 0 ? (spent.thisMonth / monthlyLimit) * 100 : 0;
    const clamped = Math.min(Math.max(pct, 0), 100);
    const dashOffset = ARC_CIRCUMFERENCE * (1 - clamped / 100);
    const tone =
      pct >= criticalThresholdPct ? 'critical' : pct >= alertThresholdPct ? 'warning' : 'safe';
    const colorClass =
      tone === 'critical'
        ? 'text-rose-500 dark:text-rose-400'
        : tone === 'warning'
          ? 'text-amber-500 dark:text-amber-400'
          : 'text-emerald-500 dark:text-emerald-400';
    return {
      pct,
      clamped,
      dashOffset,
      tone,
      colorClass,
      eta: etaExhaustLabel(spent.thisMonth, monthlyLimit, daysRemaining),
    };
  }, [budget]);

  if (isLoading) {
    return (
      <section
        className={cn('rounded-xl border border-border bg-card/60 p-6', className)}
        data-testid="budget-gauge-loading"
      >
        <div className="h-4 w-32 animate-pulse rounded bg-muted" />
        <div className="mt-4 h-32 w-full animate-pulse rounded bg-muted/50" />
      </section>
    );
  }

  if (!budget || !view) {
    return (
      <section
        className={cn('rounded-xl border border-dashed border-border bg-card/40 p-6', className)}
        data-testid="budget-gauge-empty"
      >
        <h3 className="font-quicksand text-sm font-bold text-foreground">Budget mensile</h3>
        <p className="mt-2 font-mono text-[11px] text-muted-foreground">
          Imposta budget per visualizzare il gauge e l&apos;ETA exhaust.
        </p>
      </section>
    );
  }

  return (
    <section
      className={cn('overflow-hidden rounded-xl border border-border bg-card/60 p-5', className)}
      data-testid="budget-gauge"
      aria-label={`Budget gauge ${view.pct.toFixed(1)}% utilizzato`}
    >
      <header className="mb-2 flex items-center justify-between">
        <h3 className="font-quicksand text-sm font-bold text-foreground">Budget mensile</h3>
        <span className={cn('font-mono text-[10px] uppercase tracking-wider', view.colorClass)}>
          {view.tone === 'critical'
            ? '▲ critical'
            : view.tone === 'warning'
              ? '▬ warning'
              : '▼ safe'}
        </span>
      </header>

      <div className="relative mx-auto" style={{ width: 200, height: 120 }}>
        <svg
          viewBox="0 0 200 120"
          width={200}
          height={120}
          role="img"
          aria-label="Budget gauge arc"
        >
          {/* Background arc */}
          <path
            d={arcPath()}
            fill="none"
            stroke="hsl(var(--border))"
            strokeWidth={ARC_STROKE}
            strokeLinecap="round"
            opacity={0.45}
          />
          {/* Foreground arc */}
          <path
            d={arcPath()}
            fill="none"
            stroke="currentColor"
            strokeWidth={ARC_STROKE}
            strokeLinecap="round"
            strokeDasharray={ARC_CIRCUMFERENCE}
            strokeDashoffset={view.dashOffset}
            className={view.colorClass}
            style={{ transition: 'stroke-dashoffset 280ms ease-out' }}
          />
        </svg>

        {/* Center label */}
        <div className="absolute inset-x-0 bottom-1 text-center">
          <p className="font-quicksand text-xl font-bold tabular-nums text-foreground">
            {formatCurrency(budget.spent.thisMonth)}
            <span className="text-sm font-semibold text-muted-foreground">
              {' '}
              / {formatCurrency(budget.monthlyLimit)}
            </span>
          </p>
          <p className="font-mono text-[10px] text-muted-foreground">
            {view.pct.toFixed(1)}% utilizzato
          </p>
        </div>
      </div>

      <p className="mt-2 text-center font-mono text-[11px] text-muted-foreground">
        {view.eta} · {budget.daysRemaining}gg residui
      </p>
    </section>
  );
}
