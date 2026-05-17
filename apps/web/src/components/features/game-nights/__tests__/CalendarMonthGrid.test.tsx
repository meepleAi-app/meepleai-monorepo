/**
 * Tests for CalendarMonthGrid (SP4 #1170 commit 2).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { GameNightVM } from '@/lib/game-nights/view-model';

import { CalendarMonthGrid, type CalendarMonthGridLabels } from '../CalendarMonthGrid';

const labels: CalendarMonthGridLabels = {
  prevMonth: 'Mese precedente',
  nextMonth: 'Mese successivo',
  today: 'Oggi',
  monthHeading: 'Marzo 2026',
  dayLabels: ['Lun', 'Mar', 'Mer', 'Gio', 'Ven', 'Sab', 'Dom'],
  footerCount: '10 serate · 1 annullata',
  legendEvent: 'Serata',
  legendCancelled: 'Annullata',
  legendToday: 'Oggi',
  cell: {
    todayBadge: 'Oggi',
    dayAriaLabel: (day, events) => `Giorno ${day}, ${events} eventi`,
    overflow: count => `+${count} altri`,
  },
};

function makeVM(id: string, day: number): GameNightVM {
  return {
    id,
    title: `T${id}`,
    scheduledAtIso: `2026-03-${day}T20:00:00Z`,
    day,
    month: 2,
    year: 2026,
    timeLabel: '20:00',
    durationLabel: '',
    location: '',
    gameIds: [],
    playerIds: [],
    role: 'invited',
    statusKey: 'planned',
  };
}

describe('CalendarMonthGrid', () => {
  it('renders 42 day cells and 7 day labels', () => {
    render(
      <CalendarMonthGrid
        year={2026}
        month={2}
        events={[]}
        today={new Date(2026, 2, 15)}
        labels={labels}
        onDayClick={() => {}}
      />
    );
    expect(screen.getAllByTestId('game-nights-calendar-day-cell')).toHaveLength(42);
    expect(screen.getByText('Lun')).toBeInTheDocument();
    expect(screen.getByText('Dom')).toBeInTheDocument();
  });

  it('renders month heading and footer count', () => {
    render(
      <CalendarMonthGrid
        year={2026}
        month={2}
        events={[]}
        today={new Date(2026, 2, 15)}
        labels={labels}
        onDayClick={() => {}}
      />
    );
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Marzo 2026');
    expect(screen.getByText('10 serate · 1 annullata')).toBeInTheDocument();
  });

  it('disables prev/next/today nav buttons (visual-only per spec)', () => {
    render(
      <CalendarMonthGrid
        year={2026}
        month={2}
        events={[]}
        today={new Date(2026, 2, 15)}
        labels={labels}
        onDayClick={() => {}}
      />
    );
    expect(screen.getByRole('button', { name: 'Mese precedente' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Mese successivo' })).toBeDisabled();
  });

  it('forwards onDayClick when clicking a cell with events', () => {
    const onDayClick = vi.fn();
    const events = [makeVM('e1', 10)];
    render(
      <CalendarMonthGrid
        year={2026}
        month={2}
        events={events}
        today={new Date(2026, 2, 15)}
        labels={labels}
        onDayClick={onDayClick}
      />
    );
    const cells = screen.getAllByTestId('game-nights-calendar-day-cell');
    const cellDay10 = cells.find(
      c => c.getAttribute('data-day') === '10' && c.getAttribute('data-other-month') !== 'true'
    );
    expect(cellDay10).toBeDefined();
    fireEvent.click(cellDay10!);
    expect(onDayClick).toHaveBeenCalledTimes(1);
  });

  it('marks the today cell when today matches month+year', () => {
    render(
      <CalendarMonthGrid
        year={2026}
        month={2}
        events={[]}
        today={new Date(2026, 2, 17)}
        labels={labels}
        onDayClick={() => {}}
      />
    );
    const cells = screen.getAllByTestId('game-nights-calendar-day-cell');
    const todayCell = cells.find(c => c.getAttribute('data-today') === 'true');
    expect(todayCell).toBeDefined();
    expect(todayCell?.getAttribute('data-day')).toBe('17');
  });
});
