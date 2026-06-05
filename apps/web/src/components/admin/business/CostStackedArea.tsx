'use client';

/**
 * CostStackedArea (#1838 SP5 F4-C5) — stacked area chart 30gg cost per provider.
 *
 * Mockup `sp5-admin-budget.html` riga 302-370 (Scenario C nello spec doc).
 *
 * Data via {@link useCostBreakdown}. La query restituisce `days[]` con shape
 * `{ date, providers: [{name, cost}], total }` che dobbiamo aplattare in
 * `{ date, [provider1]: cost, [provider2]: cost, total }` per Recharts.
 *
 * Provider stack order: usa `providerTotals` (sorted DESC by total) così la
 * stacking direction matches l'aspettativa "i più costosi in basso".
 *
 * Cap line: quando AppBudget configurato, mostra reference line orizzontale
 * a `monthlyLimit / 30` (daily budget cap).
 */

import { useMemo } from 'react';

import {
  Area,
  AreaChart,
  CartesianGrid,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { useBudget } from '@/hooks/useBudget';
import { useCostBreakdown } from '@/hooks/useCostBreakdown';
import type {
  CostBreakdownByProvider,
  CostBreakdownRange,
} from '@/lib/api/schemas/business-cost.schemas';
import { cn } from '@/lib/utils';

// Deterministic color palette indexed by provider order (stacking from total DESC).
// Token semantici via entity-* utilities — applied via inline `fill` prop on each Area.
const PROVIDER_COLORS = [
  'hsl(var(--c-chat))', // index 0 (largest provider)  → blue
  'hsl(var(--c-toolkit))', // index 1 → emerald
  'hsl(var(--c-agent))', // index 2 → amber
  'hsl(var(--c-event))', // index 3 → rose
  'hsl(var(--c-kb))', // index 4 → teal
  'hsl(var(--c-player))', // index 5 → purple
  'hsl(var(--c-game))', // index 6 → orange
  'hsl(var(--c-session))', // index 7 → indigo
  'hsl(var(--c-tool))', // index 8 → cyan
] as const;

function formatCurrency(value: number): string {
  return `$${value.toFixed(2)}`;
}

function formatDateShort(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString('it-IT', { month: 'short', day: 'numeric' });
}

function flattenForChart(data: CostBreakdownByProvider): Record<string, number | string>[] {
  return data.days.map(day => {
    const flat: Record<string, number | string> = {
      date: day.date,
      total: day.total,
    };
    day.providers.forEach(p => {
      flat[p.provider] = p.cost;
    });
    return flat;
  });
}

interface TooltipPayloadItem {
  name: string;
  value: number;
  color: string;
}

interface ChartTooltipProps {
  active?: boolean;
  payload?: TooltipPayloadItem[];
  label?: string;
}

function ChartTooltip({ active, payload, label }: ChartTooltipProps) {
  if (!active || !payload || payload.length === 0) return null;
  // Sort DESC by value to surface biggest contributor first.
  const sorted = [...payload].filter(p => p.name !== 'total').sort((a, b) => b.value - a.value);
  const total = sorted.reduce((acc, p) => acc + p.value, 0);

  return (
    <div className="rounded-md border border-border bg-card/95 px-3 py-2 font-mono text-[11px] shadow-lg backdrop-blur-sm">
      <p className="font-quicksand text-[12px] font-bold text-foreground">
        {label ? formatDateShort(label) : ''}
      </p>
      <p className="mt-1 text-muted-foreground">
        Totale <span className="font-bold text-foreground">{formatCurrency(total)}</span>
      </p>
      <ul className="mt-1 space-y-0.5">
        {sorted.map(p => (
          <li key={p.name} className="flex items-center gap-1.5">
            <span
              aria-hidden
              className="h-2 w-2 shrink-0 rounded-sm"
              style={{ backgroundColor: p.color }}
            />
            <span className="text-muted-foreground">{p.name}</span>
            <span className="ml-auto font-bold text-foreground tabular-nums">
              {formatCurrency(p.value)}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

interface CostStackedAreaProps {
  range: CostBreakdownRange;
  className?: string;
}

export function CostStackedArea({ range, className }: CostStackedAreaProps) {
  const { data, isLoading, isError, error } = useCostBreakdown(range);
  const { budget } = useBudget();

  const flatData = useMemo(() => (data ? flattenForChart(data) : []), [data]);
  const sortedProviders = useMemo(() => (data?.providerTotals ?? []).map(p => p.provider), [data]);

  // Daily budget cap: monthlyLimit / 30 — simple approximation matching the
  // mockup's "linea budget cap" reference. When the user selects a non-30d
  // range, we still apply daily cap because the chart is per-day.
  const dailyCap = budget ? budget.monthlyLimit / 30 : null;

  if (isLoading) {
    return (
      <section
        className={cn('overflow-hidden rounded-xl border border-border bg-card/60 p-4', className)}
        data-testid="cost-stacked-area-loading"
      >
        <div className="mb-3 h-4 w-48 animate-pulse rounded bg-muted" />
        <div className="h-64 animate-pulse rounded bg-muted/50" />
      </section>
    );
  }

  if (isError || !data) {
    return (
      <section
        className={cn('overflow-hidden rounded-xl border border-border bg-card/60 p-4', className)}
        data-testid="cost-stacked-area-error"
      >
        <h3 className="font-quicksand text-sm font-bold text-foreground">Costi per provider</h3>
        <p className="mt-2 font-mono text-[11px] text-rose-600 dark:text-rose-400">
          Errore caricamento dati: {error instanceof Error ? error.message : 'unknown'}
        </p>
      </section>
    );
  }

  if (data.days.length === 0) {
    return (
      <section
        className={cn('overflow-hidden rounded-xl border border-border bg-card/60 p-4', className)}
        data-testid="cost-stacked-area-empty"
      >
        <h3 className="font-quicksand text-sm font-bold text-foreground">
          Costi per provider · {range}
        </h3>
        <p className="mt-2 font-mono text-[11px] text-muted-foreground">
          Nessuna spesa registrata per il periodo selezionato.
        </p>
      </section>
    );
  }

  const avgDaily = data.grandTotal / data.days.length;
  const peakDay = data.days.reduce((acc, d) => (d.total > acc.total ? d : acc), data.days[0]);

  return (
    <section
      className={cn('overflow-hidden rounded-xl border border-border bg-card/60', className)}
      data-testid="cost-stacked-area"
    >
      <header className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 bg-muted/30 px-4 py-2.5">
        <div className="flex items-center gap-2">
          <h3 className="font-quicksand text-sm font-bold text-foreground">
            Costi per provider · {range}
          </h3>
          <span className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
            stacked area · $/giorno
          </span>
        </div>
        <span className="font-mono text-[10px] text-muted-foreground">
          tot {formatCurrency(data.grandTotal)} · media {formatCurrency(avgDaily)}/d · picco{' '}
          {formatCurrency(peakDay.total)} ({formatDateShort(peakDay.date)})
        </span>
      </header>

      <div className="p-4">
        <ResponsiveContainer width="100%" height={260}>
          <AreaChart data={flatData} margin={{ top: 12, right: 16, left: 0, bottom: 8 }}>
            <CartesianGrid stroke="hsl(var(--border))" strokeDasharray="3 3" />
            <XAxis
              dataKey="date"
              tickFormatter={formatDateShort}
              stroke="hsl(var(--muted-foreground))"
              fontSize={10}
              fontFamily="var(--font-mono)"
              tickLine={false}
              axisLine={{ stroke: 'hsl(var(--border))' }}
            />
            <YAxis
              tickFormatter={v => `$${v}`}
              stroke="hsl(var(--muted-foreground))"
              fontSize={10}
              fontFamily="var(--font-mono)"
              tickLine={false}
              axisLine={false}
              width={50}
            />
            <Tooltip content={<ChartTooltip />} />
            {dailyCap != null && (
              <ReferenceLine
                y={dailyCap}
                stroke="hsl(var(--c-event))"
                strokeDasharray="4 4"
                strokeWidth={1.5}
                label={{
                  value: `cap ${formatCurrency(dailyCap)}/d`,
                  position: 'insideTopRight',
                  fill: 'hsl(var(--c-event))',
                  fontSize: 10,
                  fontFamily: 'var(--font-mono)',
                }}
              />
            )}
            {sortedProviders.map((provider, idx) => (
              <Area
                key={provider}
                type="monotone"
                dataKey={provider}
                stackId="cost"
                stroke={PROVIDER_COLORS[idx % PROVIDER_COLORS.length]}
                fill={PROVIDER_COLORS[idx % PROVIDER_COLORS.length]}
                fillOpacity={0.55}
                strokeWidth={1.2}
              />
            ))}
          </AreaChart>
        </ResponsiveContainer>
      </div>
    </section>
  );
}
