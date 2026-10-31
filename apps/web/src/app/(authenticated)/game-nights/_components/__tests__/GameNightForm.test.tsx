import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';

import { GameNightForm } from '../GameNightForm';

// #4773: the edit form seeded its datetime-local field with the UTC wall clock of the stored instant and read
// the field back as local time, so every save without a touch moved the night by the browser's UTC offset.
//
// The defect only exists when the process zone is not UTC, and the CI runner is UTC. Each case therefore sets
// the zone itself and checks that it is really in force before it trusts the result: a case that quietly ran
// in UTC would pass for nothing. TZ is re-read at runtime by the default (forks) pool; `--pool=threads` cannot
// switch it, and the precondition then fails instead of passing.
const ORIGINAL_TZ = process.env.TZ;

afterEach(() => {
  if (ORIGINAL_TZ === undefined) {
    delete process.env.TZ;
  } else {
    process.env.TZ = ORIGINAL_TZ;
  }
});

interface ZoneCase {
  readonly name: string;
  readonly zone: string;
  /** `Date.getTimezoneOffset()` at `instant`: minutes UTC is ahead of local time. */
  readonly offsetMinutes: number;
  readonly instant: string;
  /** The `datetime-local` value the organizer should see for `instant`. */
  readonly field: string;
}

const CASES: readonly ZoneCase[] = [
  {
    name: 'Rome, standard time (the live repro of #4773)',
    zone: 'Europe/Rome',
    offsetMinutes: -60,
    instant: '2026-11-07T20:00:00.000Z',
    field: '2026-11-07T21:00',
  },
  {
    name: 'Rome, summer time',
    zone: 'Europe/Rome',
    offsetMinutes: -120,
    instant: '2026-08-01T19:00:00.000Z',
    field: '2026-08-01T21:00',
  },
  {
    name: 'Rome, the evening before the clocks go back',
    zone: 'Europe/Rome',
    offsetMinutes: -120,
    instant: '2026-10-24T19:00:00.000Z',
    field: '2026-10-24T21:00',
  },
  {
    name: 'Rome, the evening after the clocks go back',
    zone: 'Europe/Rome',
    offsetMinutes: -60,
    instant: '2026-10-25T19:00:00.000Z',
    field: '2026-10-25T20:00',
  },
  {
    name: 'Rome, the evening the clocks go forward',
    zone: 'Europe/Rome',
    offsetMinutes: -120,
    instant: '2026-03-29T18:00:00.000Z',
    field: '2026-03-29T20:00',
  },
  {
    name: 'New York, where the local date is the day before the UTC one',
    zone: 'America/New_York',
    offsetMinutes: 300,
    instant: '2026-11-07T02:00:00.000Z',
    field: '2026-11-06T21:00',
  },
  {
    name: 'Tokyo',
    zone: 'Asia/Tokyo',
    offsetMinutes: -540,
    instant: '2026-11-07T12:00:00.000Z',
    field: '2026-11-07T21:00',
  },
  {
    name: 'UTC (control: the defect cannot show here)',
    zone: 'UTC',
    offsetMinutes: 0,
    instant: '2026-11-07T21:00:00.000Z',
    field: '2026-11-07T21:00',
  },
];

describe('GameNightForm — editing a stored night (#4773)', () => {
  describe.each(CASES)('$name', ({ zone, offsetMinutes, instant, field }) => {
    function renderEditForm(): ReturnType<typeof vi.fn> {
      process.env.TZ = zone;
      // The zone must really be in force, or this case proves nothing.
      expect(new Date(instant).getTimezoneOffset()).toBe(offsetMinutes);

      const onSubmit = vi.fn();
      render(
        <GameNightForm
          defaultValues={{ title: 'Serata Catan', scheduledAt: instant }}
          onSubmit={onSubmit}
          submitLabel="Salva"
        />
      );
      return onSubmit;
    }

    it('shows the stored instant as the wall clock of the browser', () => {
      renderEditForm();

      expect(screen.getByLabelText('Data e Ora *')).toHaveValue(field);
    });

    it('saves an untouched form with the instant it started from', () => {
      const onSubmit = renderEditForm();

      fireEvent.click(screen.getByRole('button', { name: 'Salva' }));

      expect(onSubmit).toHaveBeenCalledTimes(1);
      expect(onSubmit.mock.calls[0][0].scheduledAt).toBe(instant);
    });

    it('reads what the organizer types as the wall clock of the browser', () => {
      const onSubmit = renderEditForm();

      // Every case starts in the evening, and no zone here changes its offset between that time and 23:15 of
      // the same local day, so the expected instant is the stored one plus the minutes typed on top of it.
      const [hours, minutes] = field.slice(11).split(':').map(Number);
      const minutesLater = 23 * 60 + 15 - (hours * 60 + minutes);
      const typed = `${field.slice(0, 11)}23:15`;
      const expected = new Date(Date.parse(instant) + minutesLater * 60_000).toISOString();

      fireEvent.change(screen.getByLabelText('Data e Ora *'), { target: { value: typed } });
      fireEvent.click(screen.getByRole('button', { name: 'Salva' }));

      expect(onSubmit.mock.calls[0][0].scheduledAt).toBe(expected);
    });
  });

  it('leaves the field empty when there is no stored night to edit', () => {
    render(<GameNightForm onSubmit={vi.fn()} />);

    expect(screen.getByLabelText('Data e Ora *')).toHaveValue('');
  });
});
