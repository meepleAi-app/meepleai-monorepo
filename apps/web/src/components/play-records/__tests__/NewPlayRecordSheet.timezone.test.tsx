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

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { NewPlayRecordSheet } from '../NewPlayRecordSheet';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// What the sheet sends to the server when it saves.
const mockCreateRecord = vi.fn();
vi.mock('@/lib/api/play-records.api', () => ({
  playRecordsApi: {
    createRecord: (request: unknown) => mockCreateRecord(request),
    addPlayer: vi.fn(),
    getRecord: vi.fn(),
    recordScore: vi.fn(),
    updateRecord: vi.fn(),
  },
}));

vi.mock('@/components/features/game-picker', () => ({
  GamePicker: () => <div data-testid="game-picker-stub" />,
}));

const ORIGINAL_ZONE = Intl.DateTimeFormat().resolvedOptions().timeZone;

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  mockCreateRecord.mockReset();
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
      // Mounted closed on an earlier day, then reopened «now»: the date can only be right if the reset on
      // reopening computes it again — and in the browser's zone.
      setNow(testCase);
      vi.setSystemTime(new Date('2026-01-15T12:00:00.000Z'));
      const { rerender } = render(<NewPlayRecordSheet open={false} onOpenChange={vi.fn()} />);

      vi.setSystemTime(new Date(testCase.now));
      rerender(<NewPlayRecordSheet open onOpenChange={vi.fn()} />);

      expect(dateInput()).toHaveValue(testCase.localDate);
    });

    it('saves the proposed day as an instant the server does not take for the future', async () => {
      // The server reads a bare `YYYY-MM-DD` as midnight UTC and refuses a date after «now»: east of UTC,
      // «today» in the browser would be refused for the first hours of the day. What goes out is the
      // instant at which the proposed day began in the browser's zone.
      setNow(testCase);
      mockCreateRecord.mockResolvedValue('record-1');
      render(<NewPlayRecordSheet open onOpenChange={vi.fn()} preselectedGameName="Catan" />);

      fireEvent.click(screen.getByTestId('next-step-btn')); // → players
      fireEvent.click(screen.getByTestId('next-step-btn')); // → summary
      fireEvent.click(screen.getByTestId('save-record-btn'));
      await waitFor(() => expect(mockCreateRecord).toHaveBeenCalledTimes(1));

      const { sessionDate } = mockCreateRecord.mock.calls[0][0] as { sessionDate: string };
      const sent = new Date(sessionDate);
      expect(sent.getTime()).toBeLessThanOrEqual(new Date(testCase.now).getTime());
      expect(sent.toLocaleDateString('en-CA')).toBe(testCase.localDate);
    });
  });

  it('refuses an emptied date with a message instead of saving or throwing', () => {
    mockCreateRecord.mockReset();
    render(<NewPlayRecordSheet open onOpenChange={vi.fn()} preselectedGameName="Catan" />);

    fireEvent.change(dateInput(), { target: { value: '' } });
    fireEvent.click(screen.getByTestId('next-step-btn')); // → players
    fireEvent.click(screen.getByTestId('next-step-btn')); // → summary
    fireEvent.click(screen.getByTestId('save-record-btn'));

    expect(screen.getByTestId('save-error')).toHaveTextContent('Scegli la data della partita');
    expect(mockCreateRecord).not.toHaveBeenCalled();
  });

  it('the cases really are moments at which the two dates differ (except the UTC control)', () => {
    // Guards the table itself, with an oracle that does not depend on the zone of the process: a case whose
    // UTC and local dates coincide would prove nothing.
    for (const c of CASES) {
      const local = new Intl.DateTimeFormat('en-CA', { timeZone: c.zone }).format(new Date(c.now));
      expect(local).toBe(c.localDate);
      expect(new Date(c.now).toISOString().slice(0, 10)).toBe(c.utcDate);
      if (c.zone !== 'UTC') expect(c.localDate).not.toBe(c.utcDate);
    }
  });
});
