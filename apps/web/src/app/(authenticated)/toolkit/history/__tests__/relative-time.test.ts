/**
 * relative-time — bucket boundary tests (Issue #3006, Task A5 EXTRA SCOPE).
 *
 * All cases use a fixed `NOW` and construct `iso` from explicit millisecond
 * offsets (via the readable MINUTE/HOUR/DAY/MONTH/YEAR constants below) so
 * the tests are deterministic and independent of wall-clock time.
 *
 * Boundary cases specifically exercise the off-by-one fix: the escalation
 * decision must be made on the ROUNDED value of the current unit, not the
 * raw fractional diff (e.g. 59.6 minutes must escalate to "1 hour", not
 * render as "60 minutes").
 */

import { describe, expect, it } from 'vitest';

import { getRelativeTimeParts } from '../_lib/relative-time';

const NOW = new Date('2026-07-15T12:00:00.000Z');

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;
const MONTH = 30 * DAY;
const YEAR = 12 * MONTH;

function isoAfter(offsetMs: number): string {
  return new Date(NOW.getTime() + offsetMs).toISOString();
}

describe('getRelativeTimeParts', () => {
  it('buckets a future diff of 10 minutes as {value: 10, unit: "minute"}', () => {
    expect(getRelativeTimeParts(isoAfter(10 * MINUTE), NOW)).toEqual({
      value: 10,
      unit: 'minute',
    });
  });

  it('buckets a past diff of 10 minutes as {value: -10, unit: "minute"}', () => {
    expect(getRelativeTimeParts(isoAfter(-10 * MINUTE), NOW)).toEqual({
      value: -10,
      unit: 'minute',
    });
  });

  it('escalates a 59.6-minute diff to {value: 1, unit: "hour"} instead of "60 minutes" (off-by-one fix)', () => {
    expect(getRelativeTimeParts(isoAfter(59.6 * MINUTE), NOW)).toEqual({
      value: 1,
      unit: 'hour',
    });
  });

  it('keeps a 58-minute diff in the minute bucket', () => {
    expect(getRelativeTimeParts(isoAfter(58 * MINUTE), NOW)).toEqual({
      value: 58,
      unit: 'minute',
    });
  });

  it('buckets a 5-hour diff as {value: 5, unit: "hour"}', () => {
    expect(getRelativeTimeParts(isoAfter(5 * HOUR), NOW)).toEqual({
      value: 5,
      unit: 'hour',
    });
  });

  it('escalates a 23.6-hour diff to {value: 1, unit: "day"}', () => {
    expect(getRelativeTimeParts(isoAfter(23.6 * HOUR), NOW)).toEqual({
      value: 1,
      unit: 'day',
    });
  });

  it('keeps a 22-hour diff in the hour bucket', () => {
    expect(getRelativeTimeParts(isoAfter(22 * HOUR), NOW)).toEqual({
      value: 22,
      unit: 'hour',
    });
  });

  it('buckets a 3-day diff as {value: 3, unit: "day"}', () => {
    expect(getRelativeTimeParts(isoAfter(3 * DAY), NOW)).toEqual({
      value: 3,
      unit: 'day',
    });
  });

  it('escalates a 29.6-day diff to {value: 1, unit: "month"}', () => {
    expect(getRelativeTimeParts(isoAfter(29.6 * DAY), NOW)).toEqual({
      value: 1,
      unit: 'month',
    });
  });

  it('keeps a 25-day diff in the day bucket', () => {
    expect(getRelativeTimeParts(isoAfter(25 * DAY), NOW)).toEqual({
      value: 25,
      unit: 'day',
    });
  });

  it('buckets a 2-month diff as {value: 2, unit: "month"}', () => {
    expect(getRelativeTimeParts(isoAfter(2 * MONTH), NOW)).toEqual({
      value: 2,
      unit: 'month',
    });
  });

  it('escalates an 11.6-month diff to {value: 1, unit: "year"}', () => {
    expect(getRelativeTimeParts(isoAfter(11.6 * MONTH), NOW)).toEqual({
      value: 1,
      unit: 'year',
    });
  });

  it('keeps a 6-month diff in the month bucket', () => {
    expect(getRelativeTimeParts(isoAfter(6 * MONTH), NOW)).toEqual({
      value: 6,
      unit: 'month',
    });
  });

  it('buckets a 2-year diff as {value: 2, unit: "year"}', () => {
    expect(getRelativeTimeParts(isoAfter(2 * YEAR), NOW)).toEqual({
      value: 2,
      unit: 'year',
    });
  });
});
