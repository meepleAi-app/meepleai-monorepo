'use client';

import React from 'react';

import { motion } from 'framer-motion';
import { Clock, TrendingDown, TrendingUp } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import type { LatencyChartProps } from './types';

/**
 * Color classes for latency bars based on performance.
 */
function getLatencyColor(value: number, threshold: number): string {
  if (value <= threshold * 0.5) return 'bg-green-500';
  if (value <= threshold) return 'bg-blue-500';
  if (value <= threshold * 1.5) return 'bg-amber-500';
  return 'bg-red-500';
}

/**
 * LatencyChart Component
 *
 * Displays latency metrics with horizontal bar chart showing
 * p50, p95, p99 percentiles and trend indicator.
 */
export function LatencyChart({
  data,
  targetThreshold = 500,
  className,
}: LatencyChartProps): React.JSX.Element {
  const maxLatency = Math.max(data.p50, data.p95, data.p99, targetThreshold);

  const getBarWidth = (value: number): number => {
    return Math.min((value / maxLatency) * 100, 100);
  };

  return (
    <Card className={cn('h-full', className)}>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold flex items-center gap-2">
          <Clock className="h-4 w-4 text-blue-500" />
          Latency
          {data.trend !== 0 && (
            <span
              className={cn(
                'text-xs flex items-center gap-1',
                data.trend < 0 ? 'text-green-500' : 'text-red-500'
              )}
            >
              {data.trend < 0 ? (
                <TrendingDown className="h-3 w-3" />
              ) : (
                <TrendingUp className="h-3 w-3" />
              )}
              {Math.abs(data.trend)}%
            </span>
          )}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {/* P50 */}
        <div className="space-y-1">
          <div className="flex items-center justify-between text-xs">
            <span className="text-muted-foreground">P50</span>
            <span className="font-mono font-medium">{data.p50}ms</span>
          </div>
          <div className="h-2 w-full bg-muted rounded-full overflow-hidden">
            <motion.div
              initial={{ width: 0 }}
              animate={{ width: `${getBarWidth(data.p50)}%` }}
              transition={{ duration: 0.5, ease: 'easeOut' }}
              className={cn('h-full rounded-full', getLatencyColor(data.p50, targetThreshold))}
            />
          </div>
        </div>

        {/* P95 */}
        <div className="space-y-1">
          <div className="flex items-center justify-between text-xs">
            <span className="text-muted-foreground">P95</span>
            <span className="font-mono font-medium">{data.p95}ms</span>
          </div>
          <div className="h-2 w-full bg-muted rounded-full overflow-hidden">
            <motion.div
              initial={{ width: 0 }}
              animate={{ width: `${getBarWidth(data.p95)}%` }}
              transition={{ duration: 0.5, ease: 'easeOut', delay: 0.1 }}
              className={cn('h-full rounded-full', getLatencyColor(data.p95, targetThreshold))}
            />
          </div>
        </div>

        {/* P99 */}
        <div className="space-y-1">
          <div className="flex items-center justify-between text-xs">
            <span className="text-muted-foreground">P99</span>
            <span className="font-mono font-medium">{data.p99}ms</span>
          </div>
          <div className="h-2 w-full bg-muted rounded-full overflow-hidden">
            <motion.div
              initial={{ width: 0 }}
              animate={{ width: `${getBarWidth(data.p99)}%` }}
              transition={{ duration: 0.5, ease: 'easeOut', delay: 0.2 }}
              className={cn('h-full rounded-full', getLatencyColor(data.p99, targetThreshold))}
            />
          </div>
        </div>

        {/* Target threshold indicator */}
        <div className="pt-2 border-t text-xs text-muted-foreground flex justify-between">
          <span>Target: &lt;{targetThreshold}ms</span>
          <span className="font-medium">Avg: {data.avg}ms</span>
        </div>
      </CardContent>
    </Card>
  );
}
