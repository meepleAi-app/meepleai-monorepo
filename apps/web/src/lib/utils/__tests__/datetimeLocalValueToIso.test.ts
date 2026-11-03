import { afterEach, describe, expect, it } from 'vitest';

import { datetimeLocalValueToIso } from '../datetimeLocal';

// The CI runner is UTC, where a value read as UTC and a value read as a wall clock give the same answer: the case
// on the zone sets its own and checks that it is in force before it trusts the result (as in #4773). TZ is re-read
// at runtime by the default (forks) pool; `--pool=threads` cannot switch it, and the precondition then fails.
const ORIGINAL_ZONE = Intl.DateTimeFormat().resolvedOptions().timeZone;

afterEach(() => {
  process.env.TZ = ORIGINAL_ZONE;
});

// #4840: the value of a `datetime-local` input is a string the browser lets the user make up, a year of more than
// four digits included, and `new Date('12345-10-30T20:00').toISOString()` throws `RangeError`. The helper answers
// `null` for what is not a date, so that a caller decides what to do instead of throwing in a handler or a render.
describe('datetimeLocalValueToIso (#4840)', () => {
  it('reads the value as a wall clock in the zone of the browser, not as UTC', () => {
    process.env.TZ = 'Europe/Rome';
    // Precondition: CET on 30 October 2026, one hour ahead of UTC.
    expect(new Date(2026, 9, 30, 20, 0).getTimezoneOffset()).toBe(-60);

    expect(datetimeLocalValueToIso('2026-10-30T20:00')).toBe('2026-10-30T19:00:00.000Z');
  });

  it.each([
    ['an empty value', ''],
    ['no value', undefined],
    ['null', null],
    ['a year of five digits, which the input lets through', '12345-10-30T20:00'],
    ['text that is not a date', 'domani sera'],
  ])('answers null for %s', (_label, value) => {
    expect(datetimeLocalValueToIso(value)).toBeNull();
  });

  it('does not throw for any of them: that is the point', () => {
    expect(() => datetimeLocalValueToIso('12345-10-30T20:00')).not.toThrow();
  });
});
