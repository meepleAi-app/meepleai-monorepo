import { afterEach, describe, expect, it } from 'vitest';

import { isoToDatetimeLocalValue } from '../datetimeLocal';

describe('isoToDatetimeLocalValue', () => {
  it.each([null, undefined, '', 'not a date'])('returns an empty string for %j', input => {
    expect(isoToDatetimeLocalValue(input)).toBe('');
  });

  it('writes the local fields of the instant, zero-padded', () => {
    // Built from local fields, so the expectation holds in whatever zone the machine runs in.
    const instant = new Date(2026, 0, 5, 9, 5).toISOString();

    expect(isoToDatetimeLocalValue(instant)).toBe('2026-01-05T09:05');
  });

  it('drops the seconds, which a datetime-local input does not carry', () => {
    const instant = new Date(2026, 10, 7, 21, 0, 42, 500).toISOString();

    expect(isoToDatetimeLocalValue(instant)).toBe('2026-11-07T21:00');
  });

  it('pads the year to four digits, the only form a datetime-local input accepts', () => {
    // Typing a year digit by digit makes the browser pass through 0002-…, 0020-…, 0202-… on the way to
    // 2026. A controlled input that writes «2-11-07T21:00» back is cleared by the browser mid-typing.
    const date = new Date(2026, 10, 7, 21, 0);
    date.setFullYear(2);

    expect(isoToDatetimeLocalValue(date.toISOString())).toBe('0002-11-07T21:00');
  });
});

// The tests above hold for `toISOString().slice(0, 16)` too whenever the process runs in UTC, as the CI
// runner does. These set the zone themselves and check that it is in force: a case that quietly ran in UTC
// would pass for nothing. TZ is re-read at runtime by the default (forks) pool, not by `--pool=threads`, where
// the precondition fails instead of passing. `delete process.env.TZ` would keep the last zone assigned, so the
// zone the process started in is captured by name and assigned back.
const ORIGINAL_ZONE = Intl.DateTimeFormat().resolvedOptions().timeZone;

afterEach(() => {
  process.env.TZ = ORIGINAL_ZONE;
});

describe.each([
  {
    zone: 'Europe/Rome',
    offsetMinutes: -60,
    instant: '2026-11-07T20:00:00.000Z',
    field: '2026-11-07T21:00',
  },
  {
    zone: 'Asia/Kolkata',
    offsetMinutes: -330,
    instant: '2026-11-07T15:30:00.000Z',
    field: '2026-11-07T21:00',
  },
  {
    zone: 'Asia/Kathmandu',
    offsetMinutes: -345,
    instant: '2026-11-07T15:15:00.000Z',
    field: '2026-11-07T21:00',
  },
  {
    zone: 'America/New_York',
    offsetMinutes: 300,
    instant: '2026-11-07T02:00:00.000Z',
    field: '2026-11-06T21:00',
  },
])('isoToDatetimeLocalValue in $zone', ({ zone, offsetMinutes, instant, field }) => {
  it('writes the wall clock there, not the UTC one', () => {
    process.env.TZ = zone;
    expect(new Date(instant).getTimezoneOffset()).toBe(offsetMinutes);

    expect(isoToDatetimeLocalValue(instant)).toBe(field);
  });
});
