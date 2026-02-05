'use client';

/**
 * UsageChart Component
 * Issue #3382: Agent Metrics Dashboard
 *
 * Displays agent usage over time as a bar chart.
 */

import { motion } from 'framer-motion';
import { useMemo } from 'react';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface UsageDataPoint {
  date: string;
  count: number;
  cost: number;
  tokens: number;
}

interface UsageChartProps {
  data: UsageDataPoint[];
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function UsageChart({ data, className }: UsageChartProps) {
  const maxCount = useMemo(() => Math.max(...data.map((d) => d.count), 1), [data]);

  // Format date for display (show only day for brevity)
  const formatDate = (dateStr: string): string => {
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  };

  if (data.length === 0) {
    return (
      <div className="flex h-64 items-center justify-center text-muted-foreground">
        No usage data available
      </div>
    );
  }

  return (
    <div className={cn('h-64', className)}>
      {/* Chart Container */}
      <div className="flex h-48 items-end gap-1">
        {data.map((point, idx) => {
          const height = (point.count / maxCount) * 100;
          return (
            <div
              key={point.date}
              className="flex flex-1 flex-col items-center gap-1"
              title={`${formatDate(point.date)}: ${point.count} invocations`}
            >
              <motion.div
                initial={{ height: 0 }}
                animate={{ height: `${height}%` }}
                transition={{ duration: 0.5, delay: idx * 0.02 }}
                className={cn(
                  'w-full min-h-[4px] rounded-t-sm',
                  'bg-blue-500 hover:bg-blue-600 transition-colors cursor-pointer'
                )}
              />
            </div>
          );
        })}
      </div>

      {/* X-Axis Labels (show every few labels to avoid crowding) */}
      <div className="mt-2 flex justify-between text-xs text-muted-foreground">
        <span>{formatDate(data[0]?.date ?? '')}</span>
        {data.length > 7 && (
          <span>{formatDate(data[Math.floor(data.length / 2)]?.date ?? '')}</span>
        )}
        <span>{formatDate(data[data.length - 1]?.date ?? '')}</span>
      </div>

      {/* Legend */}
      <div className="mt-3 flex items-center justify-between text-xs text-muted-foreground">
        <span>
          Total: {data.reduce((sum, d) => sum + d.count, 0).toLocaleString()} invocations
        </span>
        <span>
          Peak: {Math.max(...data.map((d) => d.count)).toLocaleString()}/day
        </span>
      </div>
    </div>
  );
}
