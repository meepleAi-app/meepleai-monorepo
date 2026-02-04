/**
 * AIUsageDonut Component - Issue #2790, #2850
 *
 * MeepleAI Design System:
 * - Primary (Embeddings): #d2691e (orange)
 * - Secondary (Completions): #8b5cf6 (purple)
 * - Success (OCR): #16a34a (green)
 */

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
  <div className="h-[300px] w-full animate-pulse rounded-2xl bg-muted/50 dark:bg-card" />
);

// Issue #2850: MeepleAI color palette for categories
const CATEGORY_COLORS: Record<string, string> = {
  'Embeddings': '#d2691e', // MeepleAI orange (primary)
  'Completions': '#8b5cf6', // MeepleAI purple (secondary)
  'OCR': '#16a34a',  // MeepleAI green (success)
  'Default': '#666' // MeepleAI text gray
};

function getCategoryColor(category: string): string {
   
  return CATEGORY_COLORS[category] ?? CATEGORY_COLORS['Default'];
}

export function AIUsageDonut({ data, isLoading }: AIUsageDonutProps){
  if (isLoading) {
    return <ChartSkeleton />;
  }

  if (data.length === 0) {
    return <div className="p-12 text-center text-muted-foreground">No AI usage data available</div>;
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
    // Issue #2850: MeepleAI warm card styling
    <div className="rounded-2xl border border-border/50 dark:border-border/30 bg-card/90 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none p-6 shadow-sm dark:shadow-md">
      <h3 className="mb-4 font-['Quicksand',sans-serif] text-lg font-bold text-foreground">
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
                label={({ name, value }) => `${name} ${value}%`}
              >
                {chartData.map((entry, index) => (
                  <Cell
                    key={`cell-${index}`}
                    fill={entry.color}
                    className="hover:opacity-80 transition-opacity"
                  />
                ))}
              </Pie>
              {/* Issue #2850: MeepleAI tooltip styling */}
              <Tooltip
                contentStyle={{
                  backgroundColor: '#fff',
                  border: '1px solid #e8e4d8',
                  borderRadius: '0.75rem',
                  boxShadow: '0 4px 12px rgba(139, 90, 60, 0.1)',
                }}
                formatter={(value: number, name: string, props: { payload?: { count?: number } }) => [
                  `${props?.payload?.count?.toLocaleString() ?? value} calls (${value}%)`,
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

          {/* Center text - Issue #2850: MeepleAI styling */}
          <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 text-center">
            <p className="text-sm text-muted-foreground">Total Calls</p>
            <p className="font-['Quicksand',sans-serif] text-xl font-bold text-foreground">
              {totalCalls.toLocaleString()}
            </p>
            <p className="text-xs text-muted-foreground/70">Estimated</p>
          </div>
        </div>
      </Suspense>
    </div>
  );
}
