/**
 * SessionCreateForm — step 2, the date field and the browser's time zone (#4785).
 *
 * The field is a `datetime-local` input, which has no zone: its string is a wall clock. It showed the
 * UTC wall clock of the stored date while `onChange` read what was typed as local time, so outside UTC
 * the field rewrote the hour just typed, and `max` («not in the future») was «now» in UTC.
 *
 * The defect only exists when the process zone is not UTC, and the CI runner is UTC. Each case sets the
 * zone itself and checks that it is really in force: a case that quietly ran in UTC would pass for
 * nothing. TZ is re-read at runtime by the default (forks) pool; `--pool=threads` cannot switch it, and
 * the precondition then fails instead of passing. `delete process.env.TZ` would keep the last zone
 * assigned, so the zone the process started in is captured by name and assigned back.
 */

import { fireEvent, render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { playRecordsNewMessages } from '@/__tests__/fixtures/i18n-test-messages';

import { SessionCreateForm } from '../SessionCreateForm';

// ─── Mocks (same set as SessionCreateForm.test.tsx) ───────────────────────────

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) =>
      (playRecordsNewMessages as Record<string, string>)[
        key.replace('playRecords.new.', '').replace('playRecords.edit.', '')
      ] ?? key,
  }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/lib/hooks/useMediaQuery', () => ({
  useMediaQuery: () => true,
}));

// Step 2 is the second step of the wizard.
vi.mock('@/lib/stores/play-records-store', () => ({
  usePlayRecordsStore: () => ({
    sessionCreation: { currentStep: 1 },
    nextStep: vi.fn(),
    prevStep: vi.fn(),
    resetSessionCreation: vi.fn(),
    setSessionField: vi.fn(),
  }),
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: undefined }),
}));

vi.mock('@/components/features/game-picker', () => ({
  GamePicker: () => <div data-testid="play-record-game-stub" />,
}));

vi.mock('@/components/library/AddPrivateGameForm', () => ({
  AddPrivateGameForm: () => <div />,
}));

// ─── Zones ────────────────────────────────────────────────────────────────────

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
  /** `Date.getTimezoneOffset()` at `instant`: minutes UTC is ahead of local time. */
  readonly offsetMinutes: number;
  readonly instant: string;
  /** The `datetime-local` value the user should see for `instant`. */
  readonly field: string;
}

const CASES: readonly ZoneCase[] = [
  {
    name: 'Rome, standard time',
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
    name: 'New York, where the local date is the day before the UTC one',
    zone: 'America/New_York',
    offsetMinutes: 300,
    instant: '2026-11-07T02:00:00.000Z',
    field: '2026-11-06T21:00',
  },
  {
    name: 'Kolkata, an offset of half an hour',
    zone: 'Asia/Kolkata',
    offsetMinutes: -330,
    instant: '2026-11-07T15:30:00.000Z',
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

function useZone(zone: string, instant: string, offsetMinutes: number): void {
  process.env.TZ = zone;
  // The zone must really be in force, or this case proves nothing.
  expect(new Date(instant).getTimezoneOffset()).toBe(offsetMinutes);
}

function renderForm(props: Partial<React.ComponentProps<typeof SessionCreateForm>> = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  render(
    <QueryClientProvider client={queryClient}>
      <SessionCreateForm onSubmit={vi.fn()} onCancel={vi.fn()} isSubmitting={false} {...props} />
    </QueryClientProvider>
  );
  return document.querySelector('input[type="datetime-local"]') as HTMLInputElement;
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('SessionCreateForm — the date field in the browser’s time zone (#4785)', () => {
  describe.each(CASES)('$name', ({ zone, offsetMinutes, instant, field }) => {
    it('shows a stored session date as the wall clock of the browser', () => {
      useZone(zone, instant, offsetMinutes);

      const input = renderForm({
        mode: 'edit',
        initialValues: {
          gameType: 'catalog',
          gameName: 'Wingspan',
          sessionDate: new Date(instant),
          notes: '',
          location: '',
        },
      });

      expect(input).toHaveValue(field);
    });

    it('keeps the hour the user types instead of rewriting it', () => {
      useZone(zone, instant, offsetMinutes);
      const input = renderForm();

      const typed = `${field.slice(0, 11)}18:45`;
      fireEvent.change(input, { target: { value: typed } });

      expect(input).toHaveValue(typed);
    });

    it('opens on «now» as the wall clock of the browser and caps the field at it', () => {
      useZone(zone, instant, offsetMinutes);
      vi.setSystemTime(new Date(instant));

      const input = renderForm();

      expect(input).toHaveValue(field);
      expect(input).toHaveAttribute('max', field);
    });
  });
});
