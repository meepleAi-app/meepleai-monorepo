'use client';

import { Suspense } from 'react';
import { PieChart, Pie, Cell, ResponsiveContainer, Legend, Tooltip } from 'recharts';

export type AiUsageStats = {
  model: string;
  tokens: number;
  percentage: number;
};

type AIUsageDonutProps = {
  data: AiUsageStats[];
  isLoading?: boolean;
};

const ChartSkeleton = () => (
  <div className="h-[300px] w-full animate-pulse rounded-lg bg-slate-100 dark:bg-slate-800" />
);

// Model colors mapping (Issue #2790)
const MODEL_COLORS: Record<string, string> = {
  'Claude': '#f97316', // orange-500
  'GPT-4': '#3b82f6',  // blue-500
  'Local': '#22c55e',  // green-500
  'Default': '#6b7280' // gray-500
};

function getModelColor(model: string): string {
  // Normalize model name for matching
  const normalizedModel = model.toLowerCase();

  if (normalizedModel.includes('claude')) return MODEL_COLORS['Claude'];
  if (normalizedModel.includes('gpt') || normalizedModel.includes('openai')) return MODEL_COLORS['GPT-4'];
  if (normalizedModel.includes('local') || normalizedModel.includes('llama')) return MODEL_COLORS['Local'];

  return MODEL_COLORS['Default'];
}

export function AIUsageDonut({ data, isLoading }: AIUsageDonutProps) {
  if (isLoading) {
    return <ChartSkeleton />;
  }

  if (data.length === 0) {
    return <div className="p-12 text-center text-gray-500 dark:text-gray-400">No AI usage data available</div>;
  }

  const totalTokens = data.reduce((sum, item) => sum + item.tokens, 0);

  const chartData = data.map(item => ({
    ...item,
    color: getModelColor(item.model)
  }));

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm dark:border-gray-700 dark:bg-gray-800">
      <h3 className="mb-4 text-lg font-semibold text-gray-900 dark:text-gray-100">
        AI Usage by Model
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
                nameKey="model"
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
                formatter={(value: number, name: string, props: { payload: AiUsageStats }) => [
                  `${props.payload.tokens.toLocaleString()} tokens (${value}%)`,
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
            <p className="text-sm text-gray-500 dark:text-gray-400">Total Tokens</p>
            <p className="text-xl font-bold text-gray-900 dark:text-gray-100">
              {totalTokens.toLocaleString()}
            </p>
            <p className="text-xs text-gray-400 dark:text-gray-500">Today</p>
          </div>
        </div>
      </Suspense>
    </div>
  );
}
