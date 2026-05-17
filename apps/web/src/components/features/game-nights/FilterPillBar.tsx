/**
 * FilterPillBar - v2 SP4 #1170 commit 2
 *
 * Horizontal pill bar for filtering the index by viewer role / completion.
 * Pure presentational: labels resolved by orchestrator.
 *
 * Mapped from `admin-mockups/design_files/sp4-game-nights-index.jsx` (FilterPillBar).
 */

import clsx from 'clsx';

import { FILTER_KEYS, type FilterKey } from '@/lib/game-nights/event-filter';

export interface FilterPillBarLabels {
  readonly ariaLabel: string;
  readonly all: string;
  readonly organizing: string;
  readonly invited: string;
  readonly completed: string;
}

export interface FilterPillBarProps {
  readonly value: FilterKey;
  readonly onChange: (key: FilterKey) => void;
  readonly labels: FilterPillBarLabels;
  readonly className?: string;
}

export function FilterPillBar({
  value,
  onChange,
  labels,
  className,
}: FilterPillBarProps): React.JSX.Element {
  return (
    <div
      role="group"
      aria-label={labels.ariaLabel}
      data-testid="game-nights-filter-pill-bar"
      className={clsx('flex flex-wrap gap-1.5', className)}
    >
      {FILTER_KEYS.map(key => {
        const active = key === value;
        return (
          <button
            key={key}
            type="button"
            aria-pressed={active}
            onClick={() => onChange(key)}
            className={clsx(
              'cursor-pointer whitespace-nowrap rounded-full border px-3 py-1.5',
              'font-display text-[11px] font-extrabold',
              active
                ? 'border-entity-event/40 bg-entity-event/15 text-entity-event'
                : 'border-border bg-card text-muted-foreground'
            )}
          >
            {labels[key]}
          </button>
        );
      })}
    </div>
  );
}
