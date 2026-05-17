'use client';

import { useCallback, useRef } from 'react';

import { cn } from '@/lib/utils';

export type EntityFilter = 'all' | 'games' | 'agents' | 'toolkits' | 'kbs' | 'people' | 'events';

export const ENTITY_FILTERS: ReadonlyArray<EntityFilter> = [
  'all',
  'games',
  'agents',
  'toolkits',
  'kbs',
  'people',
  'events',
] as const;

export interface EntityFilterPillBarProps {
  /** Active filter; controls aria-pressed state. */
  readonly value: EntityFilter;
  /** Called when user selects a pill. */
  readonly onChange: (next: EntityFilter) => void;
  /** Localised labels for each pill, keyed by filter id. */
  readonly labels: Readonly<Record<EntityFilter, string>>;
  /** Optional aria-label for the toolbar landmark. */
  readonly ariaLabel?: string;
}

/**
 * EntityFilterPillBar — 7-pill segmented control for /discover.
 *
 * Each pill is a button with `aria-pressed`. Supports keyboard navigation
 * (ArrowLeft / ArrowRight wraps around). Active pill rendered with
 * entity-tinted background (uses semantic foreground token; the entity color
 * accent comes from CSS class `e-{entity}` set elsewhere if desired).
 */
export function EntityFilterPillBar({
  value,
  onChange,
  labels,
  ariaLabel,
}: EntityFilterPillBarProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);

  const handleKeyDown = useCallback(
    (event: React.KeyboardEvent<HTMLDivElement>) => {
      if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
      event.preventDefault();
      const currentIndex = ENTITY_FILTERS.indexOf(value);
      if (currentIndex === -1) return;
      const delta = event.key === 'ArrowRight' ? 1 : -1;
      const nextIndex = (currentIndex + delta + ENTITY_FILTERS.length) % ENTITY_FILTERS.length;
      const next = ENTITY_FILTERS[nextIndex];
      onChange(next);
      // Move focus to the newly active pill
      const btn = containerRef.current?.querySelector<HTMLButtonElement>(
        `[data-filter-id="${next}"]`
      );
      btn?.focus();
    },
    [value, onChange]
  );

  return (
    <div
      ref={containerRef}
      role="toolbar"
      aria-label={ariaLabel}
      data-slot="entity-filter-pill-bar"
      onKeyDown={handleKeyDown}
      className="flex flex-wrap gap-2 pt-1"
    >
      {ENTITY_FILTERS.map(filter => {
        const isActive = filter === value;
        return (
          <button
            key={filter}
            type="button"
            data-filter-id={filter}
            aria-pressed={isActive}
            onClick={() => onChange(filter)}
            className={cn(
              'inline-flex shrink-0 items-center rounded-2xl border px-3 py-1 text-xs font-bold font-[Quicksand] transition-colors',
              isActive
                ? 'border-transparent bg-foreground text-background'
                : 'border-border bg-card text-muted-foreground hover:border-border-strong hover:text-foreground'
            )}
          >
            {labels[filter]}
          </button>
        );
      })}
    </div>
  );
}
