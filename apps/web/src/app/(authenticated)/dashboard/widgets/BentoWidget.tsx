'use client';

import React from 'react';

import { cn } from '@/lib/utils';

const COL_SPAN: Record<number, string> = {
  2: 'col-span-2',
  3: 'col-span-3',
  4: 'col-span-4',
  6: 'col-span-6',
};
const LG_COL_SPAN: Record<number, string> = {
  2: 'lg:col-span-2',
  3: 'lg:col-span-3',
  4: 'lg:col-span-4',
  6: 'lg:col-span-6',
  8: 'lg:col-span-8',
  12: 'lg:col-span-12',
};
const ROW_SPAN: Record<number, string> = {
  1: 'row-span-1',
  2: 'row-span-2',
  3: 'row-span-3',
  4: 'row-span-4',
  5: 'row-span-5',
  6: 'row-span-6',
};

export interface BentoWidgetProps {
  colSpan: number;
  /** col-span on the 6-col tablet grid. Defaults to min(colSpan, 6). */
  tabletColSpan?: number;
  rowSpan: number;
  accentColor?: string;
  accentBg?: string;
  className?: string;
  children: React.ReactNode;
  onClick?: () => void;
}

export function BentoWidget({
  colSpan,
  tabletColSpan,
  rowSpan,
  accentColor,
  accentBg,
  className,
  children,
  onClick,
}: BentoWidgetProps) {
  const tc = tabletColSpan ?? Math.min(colSpan, 6);
  return (
    <div
      className={cn(
        COL_SPAN[tc] ?? `col-span-${tc}`,
        LG_COL_SPAN[colSpan] ?? `lg:col-span-${colSpan}`,
        ROW_SPAN[rowSpan] ?? `row-span-${rowSpan}`,
        'rounded-xl border border-border/60 bg-card overflow-hidden p-3 transition-colors duration-150',
        onClick && 'cursor-pointer hover:bg-muted/30 hover:border-border',
        className
      )}
      style={{
        ...(accentColor ? { borderLeft: `3px solid ${accentColor}` } : {}),
        ...(accentBg ? { background: accentBg } : {}),
      }}
      onClick={onClick}
      {...(onClick
        ? {
            role: 'button' as const,
            tabIndex: 0,
            onKeyDown: (e: React.KeyboardEvent) => {
              if (e.key === 'Enter' || e.key === ' ') onClick();
            },
          }
        : {})}
    >
      {children}
    </div>
  );
}

export function WidgetLabel({ children }: { children: React.ReactNode }) {
  return (
    <p className="font-mono text-[9px] uppercase tracking-widest text-muted-foreground/50 mb-1.5">
      {children}
    </p>
  );
}
