/**
 * CategoryBreakdownChart Component
 * Issue #3723 - Ledger Dashboard and Visualization
 *
 * Horizontal bar chart showing income/expense breakdown by category.
 * Pure CSS implementation for consistency with existing patterns (ConsumptionChart).
 */

'use client';

import type { CategoryBreakdownItem } from '@/lib/api/schemas/financial-ledger.schemas';
import { cn } from '@/lib/utils';

const CATEGORY_COLORS: Record<string, { bar: string; bg: string }> = {
  Subscription: { bar: 'bg-emerald-500', bg: 'bg-emerald-50 dark:bg-emerald-900/20' },
  TokenPurchase: { bar: 'bg-blue-500', bg: 'bg-blue-50 dark:bg-blue-900/20' },
  TokenUsage: { bar: 'bg-amber-500', bg: 'bg-amber-50 dark:bg-amber-900/20' },
  PlatformFee: { bar: 'bg-violet-500', bg: 'bg-violet-50 dark:bg-violet-900/20' },
  Refund: { bar: 'bg-rose-500', bg: 'bg-rose-50 dark:bg-rose-900/20' },
  Operational: { bar: 'bg-zinc-500', bg: 'bg-zinc-50 dark:bg-zinc-900/20' },
  Marketing: { bar: 'bg-pink-500', bg: 'bg-pink-50 dark:bg-pink-900/20' },
  Infrastructure: { bar: 'bg-orange-500', bg: 'bg-orange-50 dark:bg-orange-900/20' },
  Other: { bar: 'bg-gray-400', bg: 'bg-gray-50 dark:bg-gray-900/20' },
};

export interface CategoryBreakdownChartProps {
  data: CategoryBreakdownItem[];
  loading: boolean;
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);
}

function formatCategoryLabel(category: string): string {
  return category.replace(/([A-Z])/g, ' $1').trim();
}

function ChartSkeleton() {
  return (
    <div className="space-y-3" data-testid="category-chart-skeleton">
      {Array.from({ length: 5 }).map((_, i) => (
        <div key={i} className="animate-pulse">
          <div className="h-3 w-20 bg-muted/30 rounded mb-1" />
          <div className="h-6 rounded bg-muted/20" style={{ width: `${80 - i * 12}%` }} />
        </div>
      ))}
    </div>
  );
}

export function CategoryBreakdownChart({ data, loading }: CategoryBreakdownChartProps) {
  if (loading) {
    return (
      <div className="rounded-2xl border border-zinc-200/50 dark:border-zinc-700/50 bg-white dark:bg-zinc-900 p-6">
        <h3 className="mb-4 text-lg font-semibold text-zinc-900 dark:text-zinc-100">
          Breakdown by Category
        </h3>
        <ChartSkeleton />
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <div
        className="flex h-full items-center justify-center rounded-2xl border border-zinc-200/50 dark:border-zinc-700/50 bg-white dark:bg-zinc-900 p-6 text-sm text-zinc-500"
        data-testid="category-breakdown-chart"
      >
        No category data available
      </div>
    );
  }

  const maxAmount = Math.max(...data.map((d) => d.amount));
  const sorted = [...data].sort((a, b) => b.amount - a.amount);

  return (
    <div
      className="rounded-2xl border border-zinc-200/50 dark:border-zinc-700/50 bg-white dark:bg-zinc-900 p-6"
      data-testid="category-breakdown-chart"
    >
      <h3 className="mb-4 text-lg font-semibold text-zinc-900 dark:text-zinc-100">
        Breakdown by Category
      </h3>

      <div className="space-y-3">
        {sorted.map((item) => {
          const colors = CATEGORY_COLORS[item.category] ?? CATEGORY_COLORS.Other;
          const widthPct = maxAmount > 0 ? (item.amount / maxAmount) * 100 : 0;

          return (
            <div key={`${item.category}-${item.type}`} data-testid={`category-row-${item.category}`}>
              <div className="flex items-center justify-between mb-1">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium text-zinc-700 dark:text-zinc-300">
                    {formatCategoryLabel(item.category)}
                  </span>
                  <span
                    className={cn(
                      'rounded-full px-1.5 py-0.5 text-[10px] font-medium',
                      item.type === 'Income'
                        ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400'
                        : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
                    )}
                  >
                    {item.type}
                  </span>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-sm font-bold text-zinc-800 dark:text-zinc-200">
                    {formatCurrency(item.amount)}
                  </span>
                  <span className="text-xs text-zinc-400 dark:text-zinc-500">
                    {item.percentage.toFixed(1)}%
                  </span>
                </div>
              </div>
              <div className="h-2.5 w-full rounded-full bg-zinc-100 dark:bg-zinc-800">
                <div
                  className={cn('h-full rounded-full transition-all duration-500', colors.bar)}
                  style={{ width: `${widthPct}%` }}
                  role="progressbar"
                  aria-valuenow={item.percentage}
                  aria-valuemin={0}
                  aria-valuemax={100}
                  aria-label={`${formatCategoryLabel(item.category)} ${item.type}: ${formatCurrency(item.amount)}`}
                />
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
