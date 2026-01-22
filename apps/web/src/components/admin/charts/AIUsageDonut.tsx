'use client';

import { Suspense } from 'react';
import { PieChart, Pie, Cell, ResponsiveContainer, Legend, Tooltip } from 'recharts';

export type AiUsageStats = {
  category: string;
  count: number;
};

type AIUsageDonutProps = {
  data: AiUsageStats[];
  isLoading?: boolean;
};

const ChartSkeleton = () => (
  <div className="h-[300px] w-full animate-pulse rounded-lg bg-slate-100 dark:bg-slate-800" />
);

// Category colors mapping (Issue #2790, updated for #2792)
const CATEGORY_COLORS: Record<string, string> = {
  'Embeddings': '#f97316', // orange-500
  'Completions': '#3b82f6',  // blue-500
  'OCR': '#22c55e',  // green-500
  'Default': '#6b7280' // gray-500
};

function getCategoryColor(category: string): string {
  return CATEGORY_COLORS[category] ?? CATEGORY_COLORS['Default'];
}

export function AIUsageDonut({ data, isLoading }: AIUsageDonutProps): JSX.Element {
  if (isLoading) {
    return <ChartSkeleton />;
  }

  if (data.length === 0) {
    return <div className="p-12 text-center text-gray-500 dark:text-gray-400">No AI usage data available</div>;
  }

  const totalCalls = data.reduce((sum, item) => sum + item.count, 0);

  // Calculate percentages and add colors
  const chartData = data.map(item => {
    const percentage = totalCalls > 0 ? Math.round((item.count / totalCalls) * 100) : 0;
    return {
      ...item,
      percentage,
      color: getCategoryColor(item.category),
    };
  });

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm dark:border-gray-700 dark:bg-gray-800">
      <h3 className="mb-4 text-lg font-semibold text-gray-900 dark:text-gray-100">
        AI Usage by Category
      </h3>

      <Suspense fallback={<ChartSkeleton />}>
        <div className="relative">
          <ResponsiveContainer width="100%" height={300}>
            <PieChart>
              <Pie
                data={chartData}
                cx="50%"
                cy="50%"
                innerRadius={60}
                outerRadius={100}
                dataKey="percentage"
                nameKey="category"
                label={({ name, percentage }) => `${name} ${percentage}%`}
              >
                {chartData.map((entry, index) => (
                  <Cell
                    key={`cell-${index}`}
                    fill={entry.color}
                    className="hover:opacity-80 transition-opacity"
                  />
                ))}
              </Pie>
              <Tooltip
                contentStyle={{
                  backgroundColor: '#fff',
                  border: '1px solid #e5e7eb',
                  borderRadius: '0.5rem',
                }}
                formatter={(value: number, name: string, props: { payload: AiUsageStats & { percentage: number } }) => [
                  `${props.payload.count.toLocaleString()} calls (${value}%)`,
                  name
                ]}
              />
              <Legend
                verticalAlign="middle"
                align="right"
                layout="vertical"
                wrapperStyle={{ paddingLeft: '20px' }}
              />
            </PieChart>
          </ResponsiveContainer>

          {/* Center text */}
          <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 text-center">
            <p className="text-sm text-gray-500 dark:text-gray-400">Total Calls</p>
            <p className="text-xl font-bold text-gray-900 dark:text-gray-100">
              {totalCalls.toLocaleString()}
            </p>
            <p className="text-xs text-gray-400 dark:text-gray-500">Estimated</p>
          </div>
        </div>
      </Suspense>
    </div>
  );
}
