/**
 * APIRequestsChart Component - Issue #2850
 *
 * MeepleAI Design System:
 * - Primary bar: #d2691e (orange)
 * - Grid lines: rgba(139, 90, 60, 0.1)
 * - Trend positive: #16a34a, negative: #dc2626
 */

'use client';

import { Suspense } from 'react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { TrendingUp, TrendingDown } from 'lucide-react';

// Issue #2850: MeepleAI color palette
const MEEPLE_COLORS = {
  primary: '#d2691e',    // Orange
  secondary: '#8b5cf6',  // Purple
  success: '#16a34a',    // Green
  danger: '#dc2626',     // Red
  grid: 'rgba(139, 90, 60, 0.1)',
  text: '#2d2d2d',
  border: '#e8e4d8',
};

export type ApiRequestByDay = {
  date: string;
  count: number;
};

type APIRequestsChartProps = {
  data: ApiRequestByDay[];
  isLoading?: boolean;
};

const ChartSkeleton = () => (
  <div className="h-[300px] w-full animate-pulse rounded-2xl bg-[#fef3e2] dark:bg-slate-800" />
);

function calculateTrend(data: ApiRequestByDay[]): { total: number; trend: number } {
  const total = data.reduce((sum, item) => sum + item.count, 0);

  // Calculate trend: compare first half vs second half of data
  const midpoint = Math.floor(data.length / 2);
  const firstHalf = data.slice(0, midpoint).reduce((sum, item) => sum + item.count, 0);
  const secondHalf = data.slice(midpoint).reduce((sum, item) => sum + item.count, 0);

  if (firstHalf === 0) return { total, trend: 0 };

  const trend = ((secondHalf - firstHalf) / firstHalf) * 100;
  return { total, trend };
}

export function APIRequestsChart({ data, isLoading }: APIRequestsChartProps): JSX.Element {
  if (isLoading) {
    return <ChartSkeleton />;
  }

  if (data.length === 0) {
    return <div className="p-12 text-center text-[#666] dark:text-gray-400">No API request data available</div>;
  }

  const { total, trend } = calculateTrend(data);
  const isPositiveTrend = trend >= 0;

  return (
    // Issue #2850: MeepleAI warm card styling
    <div className="rounded-2xl border border-[#e8e4d8] bg-white p-6 shadow-[0_1px_3px_rgba(139,90,60,0.05)] dark:border-gray-700 dark:bg-gray-800">
      <h3 className="mb-4 font-['Quicksand',sans-serif] text-lg font-bold text-[#2d2d2d] dark:text-gray-100">
        API Requests (Last 7 Days)
      </h3>

      <Suspense fallback={<ChartSkeleton />}>
        <ResponsiveContainer width="100%" height={300}>
          <BarChart data={data}>
            <defs>
              {/* Issue #2850: MeepleAI orange gradient */}
              <linearGradient id="barGradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={MEEPLE_COLORS.primary} stopOpacity={1} />
                <stop offset="100%" stopColor="#c05f1a" stopOpacity={1} />
              </linearGradient>
            </defs>
            {/* Issue #2850: Grid lines rgba(139, 90, 60, 0.1) */}
            <CartesianGrid strokeDasharray="3 3" stroke={MEEPLE_COLORS.grid} className="dark:stroke-gray-700" />
            <XAxis
              dataKey="date"
              stroke="#666"
              className="dark:stroke-gray-400"
              tickFormatter={(value) => {
                const date = new Date(value);
                return date.toLocaleDateString('en-US', { weekday: 'short' });
              }}
            />
            <YAxis stroke="#666" className="dark:stroke-gray-400" />
            <Tooltip
              contentStyle={{
                backgroundColor: '#fff',
                border: `1px solid ${MEEPLE_COLORS.border}`,
                borderRadius: '0.75rem',
                boxShadow: '0 4px 12px rgba(139, 90, 60, 0.1)',
              }}
              labelFormatter={(label) => new Date(label).toLocaleDateString('en-US', {
                weekday: 'short',
                month: 'short',
                day: 'numeric'
              })}
            />
            <Bar
              dataKey="count"
              fill="url(#barGradient)"
              radius={[8, 8, 0, 0]}
              className="hover:opacity-80 transition-opacity"
            />
          </BarChart>
        </ResponsiveContainer>
      </Suspense>

      {/* Footer with stats - Issue #2850: MeepleAI styling */}
      <div className="mt-4 flex items-center justify-between border-t border-[#e8e4d8] pt-4 dark:border-gray-700">
        <div>
          <p className="text-sm text-[#666] dark:text-gray-400">Total Requests</p>
          <p className="font-['Quicksand',sans-serif] text-2xl font-bold text-[#2d2d2d] dark:text-gray-100">
            {total.toLocaleString()}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {/* Issue #2850: Trend colors - using inline styles for dynamic colors */}
          {isPositiveTrend ? (
            <TrendingUp className="h-5 w-5" style={{ color: MEEPLE_COLORS.success }} />
          ) : (
            <TrendingDown className="h-5 w-5" style={{ color: MEEPLE_COLORS.danger }} />
          )}
          <div>
            <p className="text-sm text-[#666] dark:text-gray-400">vs last week</p>
            <p className={`text-lg font-bold ${isPositiveTrend ? 'text-[#16a34a]' : 'text-[#dc2626]'}`}>
              {isPositiveTrend ? '+' : ''}{trend.toFixed(1)}%
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
