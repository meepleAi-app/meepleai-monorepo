'use client';

import React from 'react';

import { cn } from '@/lib/utils';

/**
 * Navigation dots indicator
 */
export function DotsIndicator({
  total,
  current,
  onDotClick,
}: {
  total: number;
  current: number;
  onDotClick: (index: number) => void;
}) {
  // Show max 7 dots, with ellipsis for larger sets
  const maxDots = 7;
  const showEllipsis = total > maxDots;

  const getDots = () => {
    if (!showEllipsis) {
      return Array.from({ length: total }, (_, i) => i);
    }

    // Smart dot display: always show first, last, and around current
    const dots: (number | 'ellipsis')[] = [];
    const around = 1;

    for (let i = 0; i < total; i++) {
      if (i === 0 || i === total - 1 || (i >= current - around && i <= current + around)) {
        dots.push(i);
      } else if (dots[dots.length - 1] !== 'ellipsis') {
        dots.push('ellipsis');
      }
    }
    return dots;
  };

  return (
    <div
      className="flex items-center justify-center gap-2 mt-6"
      role="tablist"
      aria-label="Carousel navigation"
    >
      {getDots().map((dot, idx) =>
        dot === 'ellipsis' ? (
          <span key={`ellipsis-${idx}`} className="w-2 h-2 text-muted-foreground">
            ···
          </span>
        ) : (
          <button
            key={dot}
            onClick={() => onDotClick(dot)}
            role="tab"
            aria-selected={dot === current}
            aria-label={`Go to game ${dot + 1}`}
            className={cn(
              'rounded-full transition-all duration-300',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              dot === current
                ? 'w-8 h-2.5 bg-primary'
                : 'w-2.5 h-2.5 bg-muted-foreground/30 hover:bg-muted-foreground/60'
            )}
          />
        )
      )}
    </div>
  );
}
