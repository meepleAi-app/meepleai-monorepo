/**
 * CalendarDayCell - v2 SP4 #1170 commit 2
 *
 * Single calendar grid cell. Renders day number, optional "Oggi" badge,
 * and up to N event chips (1 mobile / 3 desktop). Overflow rendered as
 * "+N altri".
 *
 * Mapped from `admin-mockups/design_files/sp4-game-nights-index.jsx` (CalendarDayCell).
 */

import clsx from 'clsx';

import type { MonthCell } from '@/lib/game-nights/calendar-grid';
import type { GameNightVM } from '@/lib/game-nights/view-model';

export interface CalendarDayCellLabels {
  readonly todayBadge: string;
  readonly dayAriaLabel: (day: number, events: number) => string;
  readonly overflow: (count: number) => string;
}

export interface CalendarDayCellProps {
  readonly cell: MonthCell;
  readonly events: readonly GameNightVM[];
  readonly isToday: boolean;
  readonly isMobile?: boolean;
  readonly labels: CalendarDayCellLabels;
  readonly onClick?: (cell: MonthCell, events: readonly GameNightVM[]) => void;
}

export function CalendarDayCell({
  cell,
  events,
  isToday,
  isMobile = false,
  labels,
  onClick,
}: CalendarDayCellProps): React.JSX.Element {
  const maxVisible = isMobile ? 1 : 3;
  const visibleEvents = events.slice(0, maxVisible);
  const overflow = Math.max(0, events.length - maxVisible);
  const hasEvents = events.length > 0;

  const handleClick = (): void => {
    if (!cell.otherMonth && hasEvents) {
      onClick?.(cell, events);
    }
  };

  return (
    <button
      type="button"
      data-testid="game-nights-calendar-day-cell"
      data-day={cell.day}
      data-other-month={cell.otherMonth || undefined}
      data-today={isToday || undefined}
      disabled={cell.otherMonth || !hasEvents}
      aria-label={labels.dayAriaLabel(cell.day, events.length)}
      onClick={handleClick}
      className={clsx(
        'relative flex flex-col gap-1 rounded-md p-2 text-left transition-colors',
        isMobile ? 'min-h-[64px] rounded-sm p-1' : 'min-h-[110px]',
        cell.otherMonth && 'cursor-default opacity-35',
        !cell.otherMonth && hasEvents && 'cursor-pointer bg-entity-event/5',
        !cell.otherMonth && !hasEvents && 'bg-card',
        isToday ? 'border-2 border-entity-event' : 'border border-border-strong'
      )}
    >
      <div className="flex items-start">
        <span
          className={clsx(
            'font-mono text-xs font-bold',
            isToday && 'font-extrabold text-entity-event',
            cell.otherMonth && 'text-muted-foreground',
            !cell.otherMonth && !isToday && 'text-foreground'
          )}
        >
          {cell.day}
        </span>
        {isToday && !isMobile && (
          <span className="ml-auto font-mono text-[8px] font-extrabold uppercase tracking-wider text-entity-event">
            {labels.todayBadge}
          </span>
        )}
      </div>

      {visibleEvents.map(ev => {
        const isCancelled = ev.statusKey === 'cancelled';
        return (
          <div
            key={ev.id}
            className={clsx(
              'overflow-hidden text-ellipsis whitespace-nowrap rounded border-l-2 px-1.5 py-0.5',
              'font-display text-[10px] font-bold',
              isCancelled
                ? 'border-l-destructive bg-destructive/12 text-destructive line-through opacity-70'
                : 'border-l-entity-event bg-entity-event/18 text-entity-event'
            )}
          >
            <span className="font-mono font-extrabold">{ev.timeLabel}</span>
            {!isMobile && ev.title ? ` · ${ev.title}` : null}
          </div>
        );
      })}

      {overflow > 0 && (
        <div className="px-1 py-0.5 font-mono text-[9px] font-bold text-entity-event">
          {labels.overflow(overflow)}
        </div>
      )}
    </button>
  );
}
