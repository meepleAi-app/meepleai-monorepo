/**
 * GameTableSkeleton — Loading skeleton for the Game Table layout
 *
 * Mirrors the GameTableLayout grid structure with animated skeleton placeholders.
 * Shows desktop 3-col grid on sm+ and a mobile card-first layout on small screens.
 *
 * Issue #3513 — Game Table Detail
 */

'use client';

import { Skeleton } from '@/components/ui/feedback/skeleton';

export function GameTableSkeleton() {
  return (
    <div className="min-h-screen bg-[#0d1117]">
      {/* Desktop / Tablet: 3-col grid */}
      <div className="hidden sm:grid sm:grid-cols-[1fr_1.5fr_1fr] lg:grid-cols-[1.2fr_2fr_1.2fr] gap-4 p-4 lg:p-6 max-w-7xl mx-auto min-h-[80vh]">
        {/* Left zone skeleton (Tools) */}
        <div className="space-y-3" data-testid="skeleton-tools">
          <Skeleton className="h-5 w-28 bg-[#30363d]" />
          <Skeleton className="h-14 w-full rounded-lg bg-[#21262d]" />
          <Skeleton className="h-14 w-full rounded-lg bg-[#21262d]" />
          <Skeleton className="h-14 w-full rounded-lg bg-[#21262d]" />
          <Skeleton className="h-14 w-full rounded-lg bg-[#21262d]" />
        </div>

        {/* Center card skeleton */}
        <div className="flex items-start justify-center pt-8" data-testid="skeleton-card">
          <Skeleton className="w-64 h-80 rounded-2xl bg-[#21262d]" />
        </div>

        {/* Right zone skeleton (Knowledge) */}
        <div className="space-y-3" data-testid="skeleton-knowledge">
          <Skeleton className="h-5 w-28 bg-[#30363d]" />
          <Skeleton className="h-14 w-full rounded-lg bg-[#21262d]" />
          <Skeleton className="h-14 w-full rounded-lg bg-[#21262d]" />
          <Skeleton className="h-14 w-full rounded-lg bg-[#21262d]" />
        </div>
      </div>

      {/* Desktop / Tablet: Bottom sessions skeleton */}
      <div className="hidden sm:block px-4 lg:px-6 pb-4 max-w-7xl mx-auto">
        <div className="space-y-3" data-testid="skeleton-sessions">
          <Skeleton className="h-5 w-28 bg-[#30363d]" />
          <div className="flex gap-3">
            <Skeleton className="h-16 flex-1 rounded-lg bg-[#21262d]" />
            <Skeleton className="h-16 flex-1 rounded-lg bg-[#21262d]" />
            <Skeleton className="h-16 flex-1 rounded-lg bg-[#21262d]" />
            <Skeleton className="h-16 flex-1 rounded-lg bg-[#21262d]" />
          </div>
          <Skeleton className="h-10 w-full rounded-lg bg-[#21262d]" />
        </div>
      </div>

      {/* Mobile skeleton */}
      <div className="sm:hidden flex flex-col px-4 pt-4 space-y-4" data-testid="skeleton-mobile">
        <Skeleton className="w-full aspect-[3/4] max-w-[280px] mx-auto rounded-2xl bg-[#21262d]" />
        <Skeleton className="h-12 w-full rounded-lg bg-[#21262d]" />
        <Skeleton className="h-12 w-full rounded-lg bg-[#21262d]" />
        <Skeleton className="h-12 w-full rounded-lg bg-[#21262d]" />
      </div>
    </div>
  );
}
