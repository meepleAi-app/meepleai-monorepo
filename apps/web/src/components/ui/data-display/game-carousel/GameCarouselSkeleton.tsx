'use client';

import React from 'react';

import { cn } from '@/lib/utils';

export function GameCarouselSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn('relative w-full overflow-hidden py-8 md:py-12', className)}
      data-testid="game-carousel-skeleton"
    >
      {/* Header skeleton */}
      <div className="flex items-center justify-between mb-8 px-4 md:px-8">
        <div>
          <div className="h-8 w-48 bg-muted rounded animate-pulse" />
          <div className="h-4 w-32 bg-muted rounded mt-3 animate-pulse" />
        </div>
      </div>

      {/* Cards skeleton */}
      <div className="relative h-[360px] md:h-[400px]">
        <div className="absolute inset-0 flex items-center justify-center gap-4">
          {/* Side cards */}
          <div className="w-[240px] h-[300px] bg-muted rounded-2xl animate-pulse opacity-50 scale-85" />
          {/* Center card */}
          <div className="w-[300px] h-[340px] bg-muted rounded-2xl animate-pulse" />
          {/* Side cards */}
          <div className="w-[240px] h-[300px] bg-muted rounded-2xl animate-pulse opacity-50 scale-85" />
        </div>
      </div>

      {/* Dots skeleton */}
      <div className="flex items-center justify-center gap-2 mt-6">
        <div className="w-8 h-2.5 bg-primary rounded-full" />
        <div className="w-2.5 h-2.5 bg-muted rounded-full animate-pulse" />
        <div className="w-2.5 h-2.5 bg-muted rounded-full animate-pulse" />
        <div className="w-2.5 h-2.5 bg-muted rounded-full animate-pulse" />
        <div className="w-2.5 h-2.5 bg-muted rounded-full animate-pulse" />
      </div>
    </div>
  );
}
