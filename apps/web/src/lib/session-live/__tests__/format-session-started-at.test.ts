import { describe, it, expect } from 'vitest';

import { formatSessionStartedAt } from '@/lib/session-live/format-session-started-at';

// Pin locale + timeZone for determinism (the real chip uses the viewer's local zone).
const UTC = { locale: 'it-IT', timeZone: 'UTC' } as const;

describe('formatSessionStartedAt', () => {
  it('formats a UTC instant as local date + time (pinned to UTC for the test)', () => {
    // 2026-07-05T20:35:00Z → in it-IT UTC → "5 lug, 20:35" (24h)
    const out = formatSessionStartedAt('2026-07-05T20:35:00Z', UTC);
    expect(out).toMatch(/5 lug/i);
    expect(out).toContain('20:35');
  });

  it('treats a bare (offset-less) BE timestamp as UTC', () => {
    // No Z/offset → append Z → same instant as above.
    expect(formatSessionStartedAt('2026-07-05T20:35:00', UTC)).toBe(
      formatSessionStartedAt('2026-07-05T20:35:00Z', UTC)
    );
  });

  it('respects an explicit offset', () => {
    // 22:35+02:00 == 20:35Z
    expect(formatSessionStartedAt('2026-07-05T22:35:00+02:00', UTC)).toContain('20:35');
  });

  it('honors a non-UTC timeZone (local display of the same instant)', () => {
    // 20:35Z in Europe/Rome (summer, +02:00) → 22:35
    expect(
      formatSessionStartedAt('2026-07-05T20:35:00Z', { locale: 'it-IT', timeZone: 'Europe/Rome' })
    ).toContain('22:35');
  });

  it('returns empty string for an unparseable value', () => {
    expect(formatSessionStartedAt('not-a-date')).toBe('');
  });
});
