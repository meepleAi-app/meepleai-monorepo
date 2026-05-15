/**
 * Tests for CalendarDayCell (SP4 #1170 commit 2).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { MonthCell } from '@/lib/game-nights/calendar-grid';
import type { GameNightVM } from '@/lib/game-nights/view-model';

import { CalendarDayCell, type CalendarDayCellLabels } from '../CalendarDayCell';

const labels: CalendarDayCellLabels = {
  todayBadge: 'Oggi',
  dayAriaLabel: (day, events) => `Giorno ${day}, ${events} eventi`,
  overflow: count => `+${count} altri`,
};

const cell: MonthCell = { day: 15, otherMonth: false, monthOffset: 0 };

function makeVM(id: string, overrides: Partial<GameNightVM> = {}): GameNightVM {
  return {
    id,
    title: `Title ${id}`,
    scheduledAtIso: '2026-03-15T19:30:00Z',
    day: 15,
    month: 2,
    year: 2026,
    timeLabel: '20:30',
    durationLabel: '',
    location: 'Casa Marco',
    gameIds: [],
    playerIds: [],
    role: 'invited',
    statusKey: 'planned',
    ...overrides,
  };
}

describe('CalendarDayCell', () => {
  it('renders empty current-month cell (no events, button disabled)', () => {
    render(<CalendarDayCell cell={cell} events={[]} isToday={false} labels={labels} />);
    const btn = screen.getByTestId('game-nights-calendar-day-cell');
    expect(btn).toBeDisabled();
    expect(screen.getByText('15')).toBeInTheDocument();
  });

  it('renders a single event chip with time label', () => {
    render(<CalendarDayCell cell={cell} events={[makeVM('e1')]} isToday={false} labels={labels} />);
    expect(screen.getByText('20:30')).toBeInTheDocument();
  });

  it('renders 3 chips + overflow on desktop (4 events)', () => {
    const events = [
      makeVM('e1', { timeLabel: '10:00' }),
      makeVM('e2', { timeLabel: '12:00' }),
      makeVM('e3', { timeLabel: '14:00' }),
      makeVM('e4', { timeLabel: '16:00' }),
    ];
    render(<CalendarDayCell cell={cell} events={events} isToday={false} labels={labels} />);
    expect(screen.getByText('10:00')).toBeInTheDocument();
    expect(screen.getByText('12:00')).toBeInTheDocument();
    expect(screen.getByText('14:00')).toBeInTheDocument();
    expect(screen.queryByText('16:00')).not.toBeInTheDocument();
    expect(screen.getByText('+1 altri')).toBeInTheDocument();
  });

  it('shows "Oggi" badge and today border style when isToday', () => {
    render(<CalendarDayCell cell={cell} events={[]} isToday={true} labels={labels} />);
    expect(screen.getByText('Oggi')).toBeInTheDocument();
    const btn = screen.getByTestId('game-nights-calendar-day-cell');
    expect(btn.getAttribute('data-today')).toBe('true');
    expect(btn.className).toContain('border-entity-event');
  });

  it('disables otherMonth cell and applies opacity', () => {
    render(
      <CalendarDayCell
        cell={{ day: 30, otherMonth: true, monthOffset: -1 }}
        events={[]}
        isToday={false}
        labels={labels}
      />
    );
    const btn = screen.getByTestId('game-nights-calendar-day-cell');
    expect(btn).toBeDisabled();
    expect(btn.className).toContain('opacity-35');
  });

  it('renders cancelled chip with line-through class', () => {
    render(
      <CalendarDayCell
        cell={cell}
        events={[makeVM('e1', { statusKey: 'cancelled' })]}
        isToday={false}
        labels={labels}
      />
    );
    const chip = screen.getByText('20:30').parentElement;
    expect(chip?.className).toContain('line-through');
  });

  it('invokes onClick with cell and events when clicked', () => {
    const onClick = vi.fn();
    render(
      <CalendarDayCell
        cell={cell}
        events={[makeVM('e1')]}
        isToday={false}
        labels={labels}
        onClick={onClick}
      />
    );
    fireEvent.click(screen.getByTestId('game-nights-calendar-day-cell'));
    expect(onClick).toHaveBeenCalledTimes(1);
    expect(onClick.mock.calls[0][0]).toEqual(cell);
  });
});
