/**
 * MetricsGrid Component - Issue #874
 *
 * 4x4 responsive grid layout for displaying admin dashboard metrics.
 * Auto-adapts to smaller screens with responsive breakpoints.
 */

import { StatCard, type StatCardProps } from './StatCard';
import { cn } from '@/lib/utils';

export interface MetricsGridProps {
  metrics: StatCardProps[];
  className?: string;
}

export function MetricsGrid({ metrics, className }: MetricsGridProps) {
  return (
    <div
      className={cn(
        'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4',
        className
      )}
    >
      {metrics.map((metric, index) => (
        <StatCard key={`metric-${index}-${metric.label}`} {...metric} />
      ))}
    </div>
  );
}
