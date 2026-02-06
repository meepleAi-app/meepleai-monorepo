/**
 * ConsumptionChart Component
 * Issue #3692 - Token Management System
 *
 * Shows a bar chart of daily token consumption over 7 or 30 days.
 * Uses pure CSS bars (no charting library dependency).
 */

'use client';

import { useState } from 'react';

import { BarChartIcon } from 'lucide-react';

import type { TokenConsumptionPoint } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

export interface ConsumptionChartProps {
  points: TokenConsumptionPoint[];
  totalTokens: number;
  avgDailyTokens: number;
  avgDailyCost: number;
  loading?: boolean;
}

export function ConsumptionChart({
  points,
  totalTokens,
  avgDailyTokens,
  avgDailyCost,
  loading,
}: ConsumptionChartProps) {
  const [period, setPeriod] = useState<'7' | '30'>('7');

  if (loading) {
    return (
      <div className="rounded-2xl bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50 p-6 animate-pulse" data-testid="consumption-chart">
        <div className="h-5 w-40 bg-muted/30 rounded mb-4" />
        <div className="h-40 w-full bg-muted/20 rounded" />
      </div>
    );
  }

  const displayPoints = period === '7' ? points.slice(-7) : points;
  const maxTokens = Math.max(...displayPoints.map((p) => p.tokens), 1);

  return (
    <div
      className="rounded-2xl bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50 p-6"
      data-testid="consumption-chart"
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <BarChartIcon className="h-5 w-5 text-zinc-500 dark:text-zinc-400" />
          <h3 className="text-sm font-medium text-zinc-600 dark:text-zinc-300">
            Token Consumption
          </h3>
        </div>
        <div className="flex items-center bg-zinc-100 dark:bg-zinc-800 rounded-lg p-0.5">
          <button
            onClick={() => setPeriod('7')}
            className={cn(
              'px-3 py-1 text-xs font-medium rounded-md transition-colors',
              period === '7'
                ? 'bg-white dark:bg-zinc-700 text-zinc-900 dark:text-zinc-100 shadow-sm'
                : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-300'
            )}
            data-testid="consumption-period-7"
          >
            7 days
          </button>
          <button
            onClick={() => setPeriod('30')}
            className={cn(
              'px-3 py-1 text-xs font-medium rounded-md transition-colors',
              period === '30'
                ? 'bg-white dark:bg-zinc-700 text-zinc-900 dark:text-zinc-100 shadow-sm'
                : 'text-zinc-500 dark:text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-300'
            )}
            data-testid="consumption-period-30"
          >
            30 days
          </button>
        </div>
      </div>

      {/* Bar chart */}
      <div className="flex items-end gap-1 h-32 mb-4" data-testid="consumption-bars">
        {displayPoints.map((point) => {
          const height = (point.tokens / maxTokens) * 100;
          return (
            <div
              key={point.date}
              className="flex-1 flex flex-col items-center gap-1 group"
            >
              <div className="relative w-full flex items-end justify-center" style={{ height: '128px' }}>
                <div
                  className="w-full max-w-[20px] bg-amber-400/80 dark:bg-amber-500/60 rounded-t transition-all group-hover:bg-amber-500"
                  style={{ height: `${Math.max(height, 2)}%` }}
                  title={`${point.date}: ${point.tokens.toLocaleString()} tokens`}
                />
              </div>
            </div>
          );
        })}
      </div>

      {/* Stats footer */}
      <div className="grid grid-cols-3 gap-4 border-t border-zinc-200/50 dark:border-zinc-700/50 pt-3">
        <div>
          <p className="text-[10px] uppercase tracking-wider text-zinc-400 dark:text-zinc-500 mb-0.5">Total</p>
          <p className="text-sm font-semibold text-zinc-900 dark:text-zinc-100 tabular-nums" data-testid="consumption-total">
            {(totalTokens / 1000).toFixed(1)}K
          </p>
        </div>
        <div>
          <p className="text-[10px] uppercase tracking-wider text-zinc-400 dark:text-zinc-500 mb-0.5">Avg/Day</p>
          <p className="text-sm font-semibold text-zinc-900 dark:text-zinc-100 tabular-nums" data-testid="consumption-avg-daily">
            {(avgDailyTokens / 1000).toFixed(1)}K
          </p>
        </div>
        <div>
          <p className="text-[10px] uppercase tracking-wider text-zinc-400 dark:text-zinc-500 mb-0.5">Avg Cost/Day</p>
          <p className="text-sm font-semibold text-zinc-900 dark:text-zinc-100 tabular-nums" data-testid="consumption-avg-cost">
            &euro;{avgDailyCost.toFixed(2)}
          </p>
        </div>
      </div>
    </div>
  );
}
