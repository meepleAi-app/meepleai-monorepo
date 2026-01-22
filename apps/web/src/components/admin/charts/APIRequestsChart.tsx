'use client';

import { Suspense } from 'react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { TrendingUp, TrendingDown } from 'lucide-react';

export type ApiRequestByDay = {
  date: string;
  count: number;
};

type APIRequestsChartProps = {
  data: ApiRequestByDay[];
  isLoading?: boolean;
};

const ChartSkeleton = () => (
  <div className="h-[300px] w-full animate-pulse rounded-lg bg-slate-100 dark:bg-slate-800" />
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
    return <div className="p-12 text-center text-gray-500 dark:text-gray-400">No API request data available</div>;
  }

  const { total, trend } = calculateTrend(data);
  const isPositiveTrend = trend >= 0;

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm dark:border-gray-700 dark:bg-gray-800">
      <h3 className="mb-4 text-lg font-semibold text-gray-900 dark:text-gray-100">
        API Requests (Last 7 Days)
      </h3>

      <Suspense fallback={<ChartSkeleton />}>
        <ResponsiveContainer width="100%" height={300}>
          <BarChart data={data}>
            <defs>
              <linearGradient id="barGradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="#f59e0b" stopOpacity={1} /> {/* amber-500 */}
                <stop offset="100%" stopColor="#f97316" stopOpacity={1} /> {/* orange-500 */}
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" className="dark:stroke-gray-700" />
            <XAxis
              dataKey="date"
              stroke="#6b7280"
              className="dark:stroke-gray-400"
              tickFormatter={(value) => {
                const date = new Date(value);
                return date.toLocaleDateString('en-US', { weekday: 'short' });
              }}
            />
            <YAxis stroke="#6b7280" className="dark:stroke-gray-400" />
            <Tooltip
              contentStyle={{
                backgroundColor: '#fff',
                border: '1px solid #e5e7eb',
                borderRadius: '0.5rem',
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

      {/* Footer with stats */}
      <div className="mt-4 flex items-center justify-between border-t border-gray-200 pt-4 dark:border-gray-700">
        <div>
          <p className="text-sm text-gray-500 dark:text-gray-400">Total Requests</p>
          <p className="text-2xl font-bold text-gray-900 dark:text-gray-100">{total.toLocaleString()}</p>
        </div>
        <div className="flex items-center gap-2">
          {isPositiveTrend ? (
            <TrendingUp className="h-5 w-5 text-green-600" />
          ) : (
            <TrendingDown className="h-5 w-5 text-red-600" />
          )}
          <div>
            <p className="text-sm text-gray-500 dark:text-gray-400">vs last week</p>
            <p className={`text-lg font-semibold ${isPositiveTrend ? 'text-green-600' : 'text-red-600'}`}>
              {isPositiveTrend ? '+' : ''}{trend.toFixed(1)}%
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
