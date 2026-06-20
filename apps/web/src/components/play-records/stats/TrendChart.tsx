'use client';

/**
 * TrendChart — win-rate trend over the last 6 months (#2438).
 * Consumes stats.winRateTrend ({month: "YYYY-MM", winRate: 0..1}), already
 * populated by the BE. recharts AreaChart; win rate shown as 0–100%.
 *
 * Chart colors use `hsl(var(--c-game))` — the entity-game HSL triplet from
 * design-tokens-canonical.css (mirrors CostStackedArea's `hsl(var(--c-*))`).
 * The Tailwind utility `--entity-game` is not a raw CSS var, so recharts'
 * `stroke`/`fill` props read the underlying `--c-game` triplet directly.
 */

import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { useTranslation } from '@/hooks/useTranslation';
import type { PlayerStatistics } from '@/lib/api/schemas/play-records.schemas';

interface TrendChartProps {
  stats: PlayerStatistics;
}

// "YYYY-MM" → localized short month (e.g. "giu").
function formatMonthShort(month: string): string {
  const [y, m] = month.split('-').map(Number);
  if (!y || !m) return month;
  return new Date(y, m - 1, 1).toLocaleDateString('it-IT', { month: 'short' });
}

interface TrendTooltipProps {
  active?: boolean;
  payload?: { value: number }[];
  label?: string;
}

function TrendTooltip({ active, payload, label }: TrendTooltipProps) {
  if (!active || !payload || payload.length === 0) return null;
  const pct = Math.round((payload[0].value ?? 0) * 100);
  return (
    <div className="rounded-md border border-border bg-card/95 px-3 py-2 font-mono text-[11px] shadow-lg backdrop-blur-sm">
      <p className="font-display text-[12px] font-bold text-foreground">
        {label ? formatMonthShort(label) : ''}
      </p>
      <p className="mt-1 text-muted-foreground">
        win rate <span className="font-bold text-foreground">{pct}%</span>
      </p>
    </div>
  );
}

export function TrendChart({ stats }: TrendChartProps) {
  const { t } = useTranslation();
  const trend = stats.winRateTrend ?? [];
  const isEmpty = trend.length === 0;

  if (isEmpty) {
    return (
      <section
        className="rounded-lg border border-border bg-card p-4 md:p-5"
        data-testid="trend-empty"
        aria-label={t('playRecords.stats.trend.title')}
      >
        <header className="mb-4 flex items-center gap-3">
          <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md bg-entity-game/12 text-entity-game">
            📈
          </div>
          <h2 className="font-display text-base font-black text-foreground md:text-lg">
            {t('playRecords.stats.trend.title')}
          </h2>
        </header>
        <div className="flex flex-col items-center gap-2 rounded-md border border-dashed border-entity-game/30 bg-muted/30 px-4 py-6 text-center">
          <p className="text-xs text-muted-foreground">{t('playRecords.stats.trend.empty')}</p>
        </div>
      </section>
    );
  }

  // Screen-reader summary: list each month's win rate as a percentage.
  const srSummary = trend
    .map(m => `${formatMonthShort(m.month)} ${Math.round(m.winRate * 100)}%`)
    .join(', ');

  return (
    <section
      className="rounded-lg border border-border bg-card p-4 md:p-5"
      data-testid="trend-section"
      aria-label={t('playRecords.stats.trend.title')}
    >
      <header className="mb-4 flex items-center gap-3">
        <div className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-md bg-entity-game/12 text-entity-game">
          📈
        </div>
        <h2 className="font-display text-base font-black text-foreground md:text-lg">
          {t('playRecords.stats.trend.title')}
        </h2>
      </header>

      <div role="img" aria-label={`${t('playRecords.stats.trend.title')}: ${srSummary}`}>
        <ResponsiveContainer width="100%" height={220}>
          <AreaChart data={trend} margin={{ top: 12, right: 16, left: 0, bottom: 8 }}>
            <CartesianGrid stroke="hsl(var(--border))" strokeDasharray="3 3" />
            <XAxis
              dataKey="month"
              tickFormatter={formatMonthShort}
              stroke="hsl(var(--muted-foreground))"
              fontSize={10}
              tickLine={false}
              axisLine={{ stroke: 'hsl(var(--border))' }}
            />
            <YAxis
              domain={[0, 1]}
              tickFormatter={v => `${Math.round(v * 100)}%`}
              stroke="hsl(var(--muted-foreground))"
              fontSize={10}
              tickLine={false}
              axisLine={false}
              width={40}
            />
            <Tooltip content={<TrendTooltip />} />
            <Area
              type="monotone"
              dataKey="winRate"
              stroke="hsl(var(--c-game))"
              fill="hsl(var(--c-game))"
              fillOpacity={0.25}
              strokeWidth={2}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>

      {/* Visually-hidden data table for screen readers / non-visual axe checks */}
      <table className="sr-only">
        <caption>{t('playRecords.stats.trend.title')}</caption>
        <thead>
          <tr>
            <th>{t('playRecords.stats.trend.month')}</th>
            <th>{t('playRecords.stats.trend.winRate')}</th>
          </tr>
        </thead>
        <tbody>
          {trend.map(m => (
            <tr key={m.month}>
              <td>{formatMonthShort(m.month)}</td>
              <td>{Math.round(m.winRate * 100)}%</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
