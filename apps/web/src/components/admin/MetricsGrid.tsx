/**
 * MetricsGrid Component - Issue #874, #883, #2850
 *
 * Responsive grid layout for displaying admin dashboard metrics.
 * MeepleAI Design System: warm palette, smooth hover effects.
 *
 * Responsive breakpoints (Issue #2850):
 * - Desktop: 4 columns
 * - Tablet: 3 columns
 * - Mobile: 2 columns
 *
 * Features: responsive layout, loading skeleton, smooth transitions, empty state.
 */

import { BarChart3 } from 'lucide-react';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

import { StatCard, type StatCardProps } from './StatCard';

const SKELETON_COUNT = 12;

export interface MetricsGridProps {
  metrics: StatCardProps[];
  loading?: boolean;
  emptyStateMessage?: string;
  className?: string;
}

function MetricsGridSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        // Issue #2850: Responsive grid 2→3→4 columns, gap 1.5rem
        'grid grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-6',
        className
      )}
      data-testid="metrics-grid-skeleton"
    >
      {Array.from({ length: SKELETON_COUNT }).map((_, index) => (
        <Card key={`skeleton-${index}`} className="border-[#e8e4d8]">
          <CardContent className="p-8">
            <div className="flex items-start gap-4">
              <Skeleton className="h-12 w-12 rounded-xl shrink-0 bg-[#fef3e2]" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-3 w-20" />
                <Skeleton className="h-10 w-24" />
                <Skeleton className="h-4 w-28" />
              </div>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function MetricsGridEmpty({ message }: { message: string }) {
  return (
    <div
      className="flex flex-col items-center justify-center py-12 px-4 text-center"
      data-testid="metrics-grid-empty"
    >
      {/* Issue #2850: MeepleAI warm empty state */}
      <div className="p-3 bg-[#fef3e2] rounded-full mb-4">
        <BarChart3 className="h-8 w-8 text-[#d2691e]" aria-hidden="true" />
      </div>
      <p className="text-[#666] text-sm">{message}</p>
    </div>
  );
}

export function MetricsGrid({
  metrics,
  loading = false,
  emptyStateMessage = 'No metrics available',
  className,
}: MetricsGridProps) {
  if (loading) {
    return <MetricsGridSkeleton className={className} />;
  }

  if (metrics.length === 0) {
    return <MetricsGridEmpty message={emptyStateMessage} />;
  }

  return (
    <div
      className={cn(
        // Issue #2850: Responsive grid 2→3→4 columns, gap 1.5rem (6)
        'grid grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-6',
        className
      )}
      data-testid="metrics-grid"
    >
      {metrics.map((metric, index) => (
        <div
          key={`metric-${index}-${metric.label}`}
          className="transition-all duration-300 ease-in-out animate-in fade-in slide-in-from-bottom-2"
          style={{ animationDelay: `${index * 50}ms`, animationFillMode: 'both' }}
        >
          <StatCard {...metric} />
        </div>
      ))}
    </div>
  );
}
