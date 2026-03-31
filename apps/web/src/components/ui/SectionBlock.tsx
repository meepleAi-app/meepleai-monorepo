'use client';

import type { ReactNode } from 'react';

interface SectionBlockProps {
  icon: string;
  title: string;
  children: ReactNode;
  /** Conteggio opzionale mostrato accanto al titolo */
  count?: number;
  /** data-testid per lo span del conteggio */
  countTestId?: string;
}

/**
 * SectionBlock — simple section wrapper with icon + title header.
 * Replaces the old TavoloSection component.
 */
export function SectionBlock({ icon, title, children, count, countTestId }: SectionBlockProps) {
  return (
    <section>
      <div className="mb-3 flex items-center gap-2">
        <span className="text-sm" aria-hidden="true">
          {icon}
        </span>
        <h2 className="font-quicksand text-sm font-bold text-foreground">{title}</h2>
        {count !== undefined && (
          <span
            className="text-xs font-medium text-muted-foreground tabular-nums"
            data-testid={countTestId}
          >
            {count}
          </span>
        )}
        <div className="h-px flex-1 bg-border/40" />
      </div>
      {children}
    </section>
  );
}
