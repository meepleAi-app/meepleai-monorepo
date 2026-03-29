'use client';

import React from 'react';

import { cn } from '@/lib/utils';

export interface Segment {
  id: string;
  label: string;
  count?: number;
}

export interface SegmentedControlProps {
  segments: Segment[];
  activeId: string;
  onChange: (id: string) => void;
  className?: string;
}

export function SegmentedControl({
  segments,
  activeId,
  onChange,
  className,
}: SegmentedControlProps) {
  return (
    <div
      role="tablist"
      className={cn('gaming-glass inline-flex w-full gap-1 rounded-lg p-1', className)}
    >
      {segments.map(seg => {
        const active = seg.id === activeId;
        return (
          <button
            key={seg.id}
            type="button"
            role="tab"
            aria-selected={active}
            tabIndex={active ? 0 : -1}
            onClick={() => onChange(seg.id)}
            className={cn(
              'flex-1 rounded-md px-3 py-2 text-sm font-medium transition-all',
              active
                ? 'bg-white/10 text-[var(--gaming-text-primary)] shadow-sm'
                : 'text-[var(--gaming-text-secondary)] hover:text-[var(--gaming-text-primary)]'
            )}
          >
            {seg.label}
            {seg.count !== undefined && (
              <span className="ml-1 text-xs opacity-60">{seg.count}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}
