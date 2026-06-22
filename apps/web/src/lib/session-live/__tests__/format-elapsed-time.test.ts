import { describe, expect, it } from 'vitest';

import { formatElapsedTime } from '../format-elapsed-time';

describe('formatElapsedTime', () => {
  it('returns 00:00:00 for zero', () => {
    expect(formatElapsedTime(0)).toBe('00:00:00');
  });

  it('returns 00:00:00 for negative input (defensive — clock skew)', () => {
    expect(formatElapsedTime(-1000)).toBe('00:00:00');
  });

  it('returns 00:00:00 for non-finite input', () => {
    expect(formatElapsedTime(Number.NaN)).toBe('00:00:00');
    expect(formatElapsedTime(Number.POSITIVE_INFINITY)).toBe('00:00:00');
  });

  it('pads seconds under 10', () => {
    expect(formatElapsedTime(5_000)).toBe('00:00:05');
  });

  it('pads minutes and seconds', () => {
    expect(formatElapsedTime(125_000)).toBe('00:02:05');
  });

  it('formats one hour exactly', () => {
    expect(formatElapsedTime(3_600_000)).toBe('01:00:00');
  });

  it('formats hours + minutes + seconds', () => {
    // 2h 34m 56s = 9296s = 9_296_000ms
    expect(formatElapsedTime(9_296_000)).toBe('02:34:56');
  });

  it('floors sub-second milliseconds (no rounding up)', () => {
    expect(formatElapsedTime(999)).toBe('00:00:00');
    expect(formatElapsedTime(1_999)).toBe('00:00:01');
  });

  it('handles sessions ≥100 hours (extends to HHH)', () => {
    // 100h = 360_000s = 360_000_000ms
    expect(formatElapsedTime(360_000_000)).toBe('100:00:00');
  });
});
