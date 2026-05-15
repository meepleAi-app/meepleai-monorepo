/**
 * CalendarMonthGrid - v2 SP4 #1170 commit 2
 *
 * 6×7 month grid (Monday-first). Renders nav row, day labels, 42 day cells,
 * and a legend row.
 *
 * NOTE: month-nav arrows + "Oggi" reset are visual-only per mockup (no
 * fetch wiring in this commit). Buttons are `disabled` to make the
 * affordance honest.
 *
 * Mapped from `admin-mockups/design_files/sp4-game-nights-index.jsx`
 * (CalendarMonthGrid).
 */

import { buildMonthGrid, type MonthCell } from '@/lib/game-nights/calendar-grid';
import type { GameNightVM } from '@/lib/game-nights/view-model';

import { CalendarDayCell, type CalendarDayCellLabels } from './CalendarDayCell';

export interface CalendarMonthGridLabels {
  readonly prevMonth: string;
  readonly nextMonth: string;
  readonly today: string;
  readonly monthHeading: string;
  readonly dayLabels: readonly string[];
  readonly footerCount: string;
  readonly legendEvent: string;
  readonly legendCancelled: string;
  readonly legendToday: string;
  readonly cell: CalendarDayCellLabels;
}

export interface CalendarMonthGridProps {
  readonly year: number;
  readonly month: number;
  readonly events: readonly GameNightVM[];
  readonly today?: Date;
  readonly isMobile?: boolean;
  readonly labels: CalendarMonthGridLabels;
  readonly onDayClick: (cell: MonthCell, events: readonly GameNightVM[]) => void;
}

export function CalendarMonthGrid({
  year,
  month,
  events,
  today,
  isMobile = false,
  labels,
  onDayClick,
}: CalendarMonthGridProps): React.JSX.Element {
  const grid = buildMonthGrid(year, month);
  const todayDate = today ?? new Date();
  const todayDay = todayDate.getDate();
  const todayMonth = todayDate.getMonth();
  const todayYear = todayDate.getFullYear();

  return (
    <section
      data-testid="game-nights-calendar-month-grid"
      className="flex flex-col gap-3 px-4 pb-6 pt-3 md:px-8 md:pt-5 md:pb-8"
    >
      <div className="flex flex-wrap items-center gap-2.5">
        <button
          type="button"
          aria-label={labels.prevMonth}
          disabled
          className="h-8 w-8 rounded-md border border-border bg-card text-sm text-foreground"
        >
          ‹
        </button>
        <h2 className="min-w-[140px] font-display text-base font-extrabold text-foreground md:text-xl">
          {labels.monthHeading}
        </h2>
        <button
          type="button"
          aria-label={labels.nextMonth}
          disabled
          className="h-8 w-8 rounded-md border border-border bg-card text-sm text-foreground"
        >
          ›
        </button>
        <button
          type="button"
          disabled
          className="ml-1.5 rounded-md border border-entity-event/40 px-3.5 py-1.5 font-display text-xs font-extrabold text-entity-event"
        >
          {labels.today}
        </button>
        <span className="ml-auto font-mono text-xs font-bold text-muted-foreground">
          {labels.footerCount}
        </span>
      </div>

      <div className="grid grid-cols-7 gap-1">
        {labels.dayLabels.map((dayLabel, i) => (
          <div
            key={`${dayLabel}-${i}`}
            className="px-1 py-1.5 text-center font-mono text-[10px] font-extrabold uppercase tracking-wider text-muted-foreground md:text-left"
          >
            {dayLabel}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-7 gap-1">
        {grid.map((cell, idx) => {
          const cellEvents = cell.otherMonth
            ? []
            : events.filter(e => e.year === year && e.month === month && e.day === cell.day);
          const isToday =
            !cell.otherMonth && cell.day === todayDay && month === todayMonth && year === todayYear;
          return (
            <CalendarDayCell
              key={`${cell.monthOffset}-${cell.day}-${idx}`}
              cell={cell}
              events={cellEvents}
              isToday={isToday}
              isMobile={isMobile}
              labels={labels.cell}
              onClick={onDayClick}
            />
          );
        })}
      </div>

      <div className="flex flex-wrap gap-3.5 pt-2 font-mono text-[10px] font-bold text-muted-foreground">
        <span className="inline-flex items-center gap-1.5">
          <span aria-hidden="true" className="h-2 w-2 rounded-sm bg-entity-event" />
          {labels.legendEvent}
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span aria-hidden="true" className="h-2 w-2 rounded-sm bg-destructive line-through" />
          {labels.legendCancelled}
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span aria-hidden="true" className="h-2 w-2 rounded-sm border-2 border-entity-event" />
          {labels.legendToday}
        </span>
      </div>
    </section>
  );
}
