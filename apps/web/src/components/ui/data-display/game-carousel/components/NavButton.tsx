'use client';

import React from 'react';

import { ChevronLeft, ChevronRight } from 'lucide-react';

import { cn } from '@/lib/utils';

/**
 * Navigation arrow button
 */
export function NavButton({
  direction,
  onClick,
  disabled,
}: {
  direction: 'prev' | 'next';
  onClick: () => void;
  disabled?: boolean;
}) {
  const Icon = direction === 'prev' ? ChevronLeft : ChevronRight;

  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={cn(
        'absolute top-1/2 -translate-y-1/2 z-30',
        'w-12 h-12 md:w-14 md:h-14 rounded-full',
        'flex items-center justify-center',
        'bg-card/95 backdrop-blur-sm border border-border/50',
        'shadow-lg hover:shadow-xl',
        'text-foreground hover:text-primary',
        'transition-all duration-300 ease-out',
        'hover:scale-110 active:scale-95',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        'disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:scale-100',
        // Glow effect on hover
        'hover:border-primary/50',
        'dark:hover:shadow-[0_0_20px_hsl(25_95%_45%/0.3)]',
        direction === 'prev' ? 'left-2 md:left-4' : 'right-2 md:right-4'
      )}
      aria-label={direction === 'prev' ? 'Previous game' : 'Next game'}
    >
      <Icon className="w-6 h-6 md:w-7 md:h-7" />
    </button>
  );
}
