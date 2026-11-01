/**
 * NewPlayRecordSheet — the date proposed for the game, in the browser's time zone (#4789).
 *
 * The sheet proposed «today» as `new Date().toISOString().slice(0, 10)`, the UTC calendar date, for an
 * `<input type="date">` that shows a LOCAL calendar date. The two differ for a few hours a day: in Rome
 * shortly after midnight the sheet proposed yesterday, in New York in the evening it proposed tomorrow.
 *
 * The defect only exists when the process zone is not UTC, and the CI runner is UTC. Each case sets the
 * zone itself and checks that it is really in force: a case that quietly ran in UTC would pass for
 * nothing. TZ is re-read at runtime by the default (forks) pool; `--pool=threads` cannot switch it, and
 * the precondition then fails instead of passing. `delete process.env.TZ` would keep the last zone
 * assigned, so the zone the process started in is captured by name and assigned back.
 */

import { render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { NewPlayRecordSheet } from '../NewPlayRecordSheet';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/features/game-picker', () => ({
  GamePicker: () => <div data-testid="game-picker-stub" />,
}));

const ORIGINAL_ZONE = Intl.DateTimeFormat().resolvedOptions().timeZone;

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
});

afterEach(() => {
  vi.useRealTimers();
  process.env.TZ = ORIGINAL_ZONE;
});

interface ZoneCase {
  readonly name: string;
  readonly zone: string;
  /** `Date.getTimezoneOffset()` at `now`: minutes UTC is ahead of local time. */
  readonly offsetMinutes: number;
  /** A moment at which the UTC calendar date and the local one differ. */
  readonly now: string;
  /** The local calendar date at `now`. */
  readonly localDate: string;
  /** The UTC calendar date at `now`: what the sheet used to propose. */
  readonly utcDate: string;
}

const CASES: readonly ZoneCase[] = [
  {
    name: 'Rome, standard time, shortly after midnight',
    zone: 'Europe/Rome',
    offsetMinutes: -60,
    now: '2026-11-06T23:30:00.000Z',
    localDate: '2026-11-07',
    utcDate: '2026-11-06',
  },
  {
    name: 'Rome, summer time, shortly after midnight',
    zone: 'Europe/Rome',
    offsetMinutes: -120,
    now: '2026-08-01T22:30:00.000Z',
    localDate: '2026-08-02',
    utcDate: '2026-08-01',
  },
  {
    name: 'New York, in the evening',
    zone: 'America/New_York',
    offsetMinutes: 300,
    now: '2026-11-07T01:30:00.000Z',
    localDate: '2026-11-06',
    utcDate: '2026-11-07',
  },
  {
    name: 'Tokyo, in the small hours',
    zone: 'Asia/Tokyo',
    offsetMinutes: -540,
    now: '2026-11-06T16:00:00.000Z',
    localDate: '2026-11-07',
    utcDate: '2026-11-06',
  },
  {
    name: 'UTC (control: the two dates cannot differ here)',
    zone: 'UTC',
    offsetMinutes: 0,
    now: '2026-11-07T12:00:00.000Z',
    localDate: '2026-11-07',
    utcDate: '2026-11-07',
  },
];

function setNow({ zone, offsetMinutes, now }: ZoneCase): void {
  process.env.TZ = zone;
  // The zone must really be in force, or this case proves nothing.
  expect(new Date(now).getTimezoneOffset()).toBe(offsetMinutes);
  vi.setSystemTime(new Date(now));
}

const dateInput = (): HTMLInputElement =>
  screen.getByTestId('session-date-input') as HTMLInputElement;

describe('NewPlayRecordSheet — «today» as the calendar date of the browser (#4789)', () => {
  describe.each(CASES)('$name', testCase => {
    it('proposes the local date when the sheet opens', () => {
      setNow(testCase);

      render(<NewPlayRecordSheet open onOpenChange={vi.fn()} />);

      expect(dateInput()).toHaveValue(testCase.localDate);
    });

    it('proposes the local date again when the sheet is reopened', () => {
      // Closed at a moment long before, then reopened «now»: the reset on reopening must not use the UTC
      // date either.
      setNow(testCase);
      const { rerender } = render(<NewPlayRecordSheet open={false} onOpenChange={vi.fn()} />);

      rerender(<NewPlayRecordSheet open onOpenChange={vi.fn()} />);

      expect(dateInput()).toHaveValue(testCase.localDate);
    });
  });

  it('the cases really are moments at which the two dates differ (except the UTC control)', () => {
    // Guards the table itself: a case whose UTC and local dates coincide would prove nothing.
    for (const c of CASES.filter(c => c.zone !== 'UTC')) {
      expect(c.localDate).not.toBe(c.utcDate);
      expect(new Date(c.now).toISOString().slice(0, 10)).toBe(c.utcDate);
    }
  });
});
