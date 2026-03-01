/**
 * LoadingSkeleton - Loading state for EntityListView
 *
 * Skeleton loader that adapts to current view mode (Grid/List/Carousel).
 *
 * @module components/ui/data-display/entity-list-view/components/loading-skeleton
 */

'use client';

import React from 'react';

import { cn } from '@/lib/utils';

import { Skeleton } from '../../../feedback/skeleton';
import { GameCarouselSkeleton } from '../../game-carousel';
import { MeepleCardSkeleton } from '../../meeple-card';

import type { ViewMode } from '../entity-list-view.types';

export interface LoadingSkeletonProps {
  /** Current view mode */
  mode: ViewMode;
  /** Number of skeleton cards to show (default: 8 for grid/list, ignored for carousel) */
  count?: number;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}

/**
 * Loading skeleton component that adapts to view mode
 */
export const LoadingSkeleton = React.memo(function LoadingSkeleton({
  mode,
  count = 8,
  className,
  'data-testid': testId,
}: LoadingSkeletonProps) {
  // Table skeleton
  if (mode === 'table') {
    return (
      <div
        className={cn('rounded-md border', className)}
        data-testid={testId || 'loading-skeleton-table'}
        role="status"
        aria-label="Loading"
      >
        <div className="flex gap-4 p-3 border-b bg-muted/20">
          <Skeleton className="h-4 w-[200px]" />
          <Skeleton className="h-4 w-[150px]" />
          <Skeleton className="h-4 w-[80px]" />
        </div>
        {Array.from({ length: Math.min(count, 8) }).map((_, idx) => (
          <div key={idx} className="flex gap-4 p-3 border-b last:border-b-0">
            <div className="flex items-center gap-2 w-[200px]">
              <Skeleton className="h-8 w-8 rounded" />
              <Skeleton className="h-4 flex-1" />
            </div>
            <Skeleton className="h-4 w-[150px]" />
            <Skeleton className="h-4 w-[80px]" />
          </div>
        ))}
        <span className="sr-only">Loading...</span>
      </div>
    );
  }

  // Carousel has its own skeleton
  if (mode === 'carousel') {
    return <GameCarouselSkeleton className={className} />;
  }

  // Grid skeleton
  if (mode === 'grid') {
    return (
      <div
        className={cn(
          'grid gap-4',
          'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4',
          className
        )}
        data-testid={testId || 'loading-skeleton-grid'}
        role="status"
        aria-label="Loading"
      >
        {Array.from({ length: count }).map((_, idx) => (
          <MeepleCardSkeleton key={idx} variant="grid" />
        ))}
        <span className="sr-only">Loading...</span>
      </div>
    );
  }

  // List skeleton
  return (
    <div
      className={cn('space-y-2', className)}
      data-testid={testId || 'loading-skeleton-list'}
      role="status"
      aria-label="Loading"
    >
      {Array.from({ length: count }).map((_, idx) => (
        <MeepleCardSkeleton key={idx} variant="list" />
      ))}
      <span className="sr-only">Loading...</span>
    </div>
  );
});
