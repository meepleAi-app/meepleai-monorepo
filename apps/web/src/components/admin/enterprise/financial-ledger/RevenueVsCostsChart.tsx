/**
 * RevenueVsCostsChart Component
 * Issue #3723 - Ledger Dashboard and Visualization
 *
 * 12-month bar chart showing revenue vs costs side by side.
 * Uses Recharts with MeepleAI color palette.
 */

'use client';

import { Suspense } from 'react';

import { TrendingUpIcon, TrendingDownIcon } from 'lucide-react';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Legend,
} from 'recharts';

import type { MonthlyRevenueData } from '@/lib/api/schemas/financial-ledger.schemas';

const COLORS = {
  revenue: '#16a34a',
  revenueDark: '#15803d',
  costs: '#dc2626',
  costsDark: '#b91c1c',
  grid: 'rgba(139, 90, 60, 0.1)',
  border: '#e8e4d8',
};

export interface RevenueVsCostsChartProps {
  data: MonthlyRevenueData[];
  loading: boolean;
}

function ChartSkeleton() {
  return (
    <div className="h-[320px] w-full animate-pulse rounded-2xl bg-muted/30 dark:bg-zinc-800/30" data-testid="revenue-chart-skeleton" />
  );
}

function calculateTrend(data: MonthlyRevenueData[]): { totalRevenue: number; totalCosts: number; trend: number } {
  const totalRevenue = data.reduce((sum, d) => sum + d.revenue, 0);
  const totalCosts = data.reduce((sum, d) => sum + d.costs, 0);

  if (data.length < 2) return { totalRevenue, totalCosts, trend: 0 };

  const mid = Math.floor(data.length / 2);
  const firstHalfProfit = data.slice(0, mid).reduce((s, d) => s + (d.revenue - d.costs), 0);
  const secondHalfProfit = data.slice(mid).reduce((s, d) => s + (d.revenue - d.costs), 0);

  if (firstHalfProfit === 0) return { totalRevenue, totalCosts, trend: 0 };

  const trend = ((secondHalfProfit - firstHalfProfit) / Math.abs(firstHalfProfit)) * 100;
  return { totalRevenue, totalCosts, trend };
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(value);
}

export function RevenueVsCostsChart({ data, loading }: RevenueVsCostsChartProps) {
  if (loading) {
    return <ChartSkeleton />;
  }

  if (data.length === 0) {
    return (
      <div className="flex h-[320px] items-center justify-center rounded-2xl border border-zinc-200/50 dark:border-zinc-700/50 bg-white dark:bg-zinc-900 text-sm text-zinc-500">
        No revenue data available
      </div>
    );
  }

  const { totalRevenue, totalCosts, trend } = calculateTrend(data);
  const isPositiveTrend = trend >= 0;

  return (
    <div
      className="rounded-2xl border border-zinc-200/50 dark:border-zinc-700/50 bg-white dark:bg-zinc-900 p-6"
      data-testid="revenue-vs-costs-chart"
    >
      <h3 className="mb-4 text-lg font-semibold text-zinc-900 dark:text-zinc-100">
        Revenue vs Costs (12 Months)
      </h3>

      <Suspense fallback={<ChartSkeleton />}>
        <ResponsiveContainer width="100%" height={320}>
          <BarChart data={data} barGap={2} barCategoryGap="20%">
            <defs>
              <linearGradient id="revenueGradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={COLORS.revenue} stopOpacity={0.9} />
                <stop offset="100%" stopColor={COLORS.revenueDark} stopOpacity={0.7} />
              </linearGradient>
              <linearGradient id="costsGradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={COLORS.costs} stopOpacity={0.9} />
                <stop offset="100%" stopColor={COLORS.costsDark} stopOpacity={0.7} />
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke={COLORS.grid} />
            <XAxis
              dataKey="month"
              stroke="#666"
              className="dark:stroke-gray-400"
              tick={{ fontSize: 12 }}
            />
            <YAxis
              stroke="#666"
              className="dark:stroke-gray-400"
              tick={{ fontSize: 12 }}
              tickFormatter={(v) => `${(v / 1000).toFixed(0)}k`}
            />
            <Tooltip
              contentStyle={{
                backgroundColor: '#fff',
                border: `1px solid ${COLORS.border}`,
                borderRadius: '0.75rem',
                boxShadow: '0 4px 12px rgba(139, 90, 60, 0.1)',
                fontSize: '0.875rem',
              }}
              formatter={(value: number, name: string) => [
                formatCurrency(value),
                name === 'revenue' ? 'Revenue' : 'Costs',
              ]}
            />
            <Legend
              wrapperStyle={{ fontSize: '0.875rem', paddingTop: '0.5rem' }}
              formatter={(value) => (value === 'revenue' ? 'Revenue' : 'Costs')}
            />
            <Bar dataKey="revenue" fill="url(#revenueGradient)" radius={[4, 4, 0, 0]} />
            <Bar dataKey="costs" fill="url(#costsGradient)" radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </Suspense>

      <div className="mt-4 flex items-center justify-between border-t border-zinc-200/50 dark:border-zinc-700/50 pt-4">
        <div className="flex gap-6">
          <div>
            <p className="text-xs text-zinc-500 dark:text-zinc-400">Total Revenue</p>
            <p className="text-lg font-bold text-emerald-600 dark:text-emerald-400">{formatCurrency(totalRevenue)}</p>
          </div>
          <div>
            <p className="text-xs text-zinc-500 dark:text-zinc-400">Total Costs</p>
            <p className="text-lg font-bold text-red-600 dark:text-red-400">{formatCurrency(totalCosts)}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {isPositiveTrend ? (
            <TrendingUpIcon className="h-5 w-5 text-emerald-500" />
          ) : (
            <TrendingDownIcon className="h-5 w-5 text-red-500" />
          )}
          <div className="text-right">
            <p className="text-xs text-zinc-500 dark:text-zinc-400">Profit Trend</p>
            <p className={`text-sm font-bold ${isPositiveTrend ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'}`}>
              {isPositiveTrend ? '+' : ''}{trend.toFixed(1)}%
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
