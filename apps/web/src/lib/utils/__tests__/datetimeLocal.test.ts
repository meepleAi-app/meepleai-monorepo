import { describe, expect, it } from 'vitest';

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
});
