'use client';

import React from 'react';

import { ChevronLeft } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface MobileHeaderProps {
  title: string;
  subtitle?: string;
  onBack?: () => void;
  rightActions?: React.ReactNode;
  className?: string;
}

export function MobileHeader({
  title,
  subtitle,
  onBack,
  rightActions,
  className,
}: MobileHeaderProps) {
  return (
    <header
      className={cn(
        'sticky top-0 z-30 lg:hidden',
        'flex items-center gap-3',
        'h-[var(--size-mobile-header)] px-4',
        'bg-[var(--gaming-bg-base)]/95 backdrop-blur-sm',
        'border-b border-[var(--gaming-border-glass)]',
        className
      )}
    >
      {onBack && (
        <button
          onClick={onBack}
          aria-label="Torna indietro"
          className="flex h-9 w-9 items-center justify-center rounded-full text-[var(--gaming-text-secondary)] hover:bg-white/5"
        >
          <ChevronLeft className="h-5 w-5" />
        </button>
      )}

      <div className="flex-1 min-w-0">
        <h1 className="truncate text-base font-semibold text-[var(--gaming-text-primary)]">
          {title}
        </h1>
        {subtitle && (
          <p className="truncate text-xs text-[var(--gaming-text-secondary)]">{subtitle}</p>
        )}
      </div>

      {rightActions && <div className="flex items-center gap-2">{rightActions}</div>}
    </header>
  );
}
