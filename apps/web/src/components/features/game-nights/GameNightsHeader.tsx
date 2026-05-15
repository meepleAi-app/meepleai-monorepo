/**
 * GameNightsHeader - v2 SP4 #1170 commit 2
 *
 * Page header for /game-nights index: kicker pill + title + count line +
 * "+ Nuova serata" CTA, plus a calendar/list tablist and embedded
 * `FilterPillBar`. Pure presentational: orchestrator owns state.
 *
 * Tablist keyboard nav uses `useTablistKeyboardNav` (WAI-ARIA APG horizontal).
 *
 * Mapped from `admin-mockups/design_files/sp4-game-nights-index.jsx` (Header).
 */

'use client';

import clsx from 'clsx';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';
import type { FilterKey } from '@/lib/game-nights/event-filter';

import { FilterPillBar, type FilterPillBarLabels } from './FilterPillBar';

export type GameNightsView = 'calendar' | 'list';

const VIEW_ORDER: ReadonlyArray<GameNightsView> = ['calendar', 'list'];

export interface GameNightsHeaderLabels {
  readonly kicker: string;
  readonly title: string;
  readonly countLine: string;
  readonly ctaNew: string;
  readonly viewTablistAriaLabel: string;
  readonly viewCalendar: string;
  readonly viewList: string;
  readonly filter: FilterPillBarLabels;
}

export interface GameNightsHeaderProps {
  readonly view: GameNightsView;
  readonly onViewChange: (view: GameNightsView) => void;
  readonly filter: FilterKey;
  readonly onFilterChange: (key: FilterKey) => void;
  readonly onCreate: () => void;
  readonly labels: GameNightsHeaderLabels;
  readonly isMobile?: boolean;
  readonly className?: string;
}

export function GameNightsHeader({
  view,
  onViewChange,
  filter,
  onFilterChange,
  onCreate,
  labels,
  isMobile: _isMobile = false,
  className,
}: GameNightsHeaderProps): React.JSX.Element {
  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<GameNightsView>({
    orderedKeys: VIEW_ORDER,
    onChange: onViewChange,
  });

  return (
    <header
      data-testid="game-nights-header"
      className={clsx(
        'flex flex-col gap-3 bg-background px-4 pt-3.5 md:gap-4 md:px-8 md:pt-6',
        className
      )}
    >
      <div className="flex flex-col items-start gap-3 md:flex-row md:flex-wrap md:items-end">
        <div className="min-w-0 flex-1">
          <span className="inline-flex items-center gap-2 rounded-full bg-entity-event/12 px-2.5 py-1 font-mono text-[10px] font-extrabold uppercase tracking-wider text-entity-event">
            <span aria-hidden="true">📅</span>
            <span>{labels.kicker}</span>
          </span>
          <h1 className="mt-1 font-display text-2xl font-extrabold leading-tight tracking-tight text-foreground md:text-3xl">
            {labels.title}
          </h1>
          <p className="mt-1 font-mono text-xs font-bold text-muted-foreground">
            {labels.countLine}
          </p>
        </div>

        <button
          type="button"
          onClick={onCreate}
          className={clsx(
            'inline-flex items-center justify-center gap-1.5 self-stretch rounded-md px-4 py-2.5 md:self-auto',
            'bg-gradient-to-br from-entity-event to-entity-session text-white shadow-md shadow-entity-event/30',
            'font-display text-sm font-extrabold'
          )}
        >
          {labels.ctaNew}
        </button>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3 md:justify-start">
        <div
          role="tablist"
          aria-label={labels.viewTablistAriaLabel}
          className="inline-flex items-center gap-1"
        >
          {VIEW_ORDER.map(key => {
            const active = key === view;
            const label = key === 'calendar' ? labels.viewCalendar : labels.viewList;
            return (
              <button
                key={key}
                ref={node => {
                  if (node) tabRefs.current.set(key, node);
                  else tabRefs.current.delete(key);
                }}
                type="button"
                role="tab"
                aria-selected={active}
                tabIndex={active ? 0 : -1}
                onClick={() => onViewChange(key)}
                onKeyDown={e => handleKeyDown(e, key)}
                className={clsx(
                  'rounded-t-md px-3 py-2 font-display text-xs font-extrabold transition-colors',
                  active
                    ? 'border-b-2 border-entity-event text-entity-event'
                    : 'border-b-2 border-transparent text-muted-foreground'
                )}
              >
                {label}
              </button>
            );
          })}
        </div>

        <FilterPillBar
          value={filter}
          onChange={onFilterChange}
          labels={labels.filter}
          className="md:ml-auto"
        />
      </div>
    </header>
  );
}
