import { describe, expect, it } from 'vitest';

import { datetimeLocalValueToIso } from '../datetimeLocal';

// #4840: the value of a `datetime-local` input is a string the browser lets the user make up, a year of more than
// four digits included, and `new Date('12345-10-30T20:00').toISOString()` throws `RangeError`. The helper answers
// `null` for what is not a date, so that a caller decides what to do instead of throwing in a handler or a render.
describe('datetimeLocalValueToIso (#4840)', () => {
  it('reads the value as a wall clock in the zone of the browser, like the input shows it', () => {
    expect(datetimeLocalValueToIso('2026-10-30T20:00')).toBe(
      new Date(2026, 9, 30, 20, 0).toISOString()
    );
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
