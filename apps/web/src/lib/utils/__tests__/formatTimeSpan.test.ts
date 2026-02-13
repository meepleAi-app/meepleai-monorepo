/**
 * Tests for TimeSpan formatting utilities (Issue #4219)
 */

import { describe, it, expect } from 'vitest';
import { formatTimeSpan, formatETA } from '../formatTimeSpan';

describe('formatTimeSpan', () => {
  it('formats hours and minutes', () => {
    expect(formatTimeSpan('01:30:00')).toBe('1h 30m');
  });

  it('formats minutes and seconds', () => {
    expect(formatTimeSpan('00:03:45')).toBe('3m 45s');
  });

  it('formats seconds only', () => {
    expect(formatTimeSpan('00:00:45')).toBe('45s');
  });

  it('formats under 10 seconds as "< 1m"', () => {
    expect(formatTimeSpan('00:00:05')).toBe('< 1m');
  });

  it('handles null input', () => {
    expect(formatTimeSpan(null)).toBe('-');
  });

  it('handles undefined input', () => {
    expect(formatTimeSpan(undefined)).toBe('-');
  });

  it('formats zero duration', () => {
    expect(formatTimeSpan('00:00:00')).toBe('0s');
  });

  it('handles .NET TimeSpan with fractional seconds', () => {
    expect(formatTimeSpan('00:01:23.5000000')).toBe('1m 24s'); // Math.round(23.5) = 24
  });

  it('omits seconds when duration >= 1 hour', () => {
    expect(formatTimeSpan('02:15:45')).toBe('2h 15m');
  });
});

describe('formatETA', () => {
  it('adds "~" prefix to formatted time', () => {
    expect(formatETA('00:03:45')).toBe('~3m 45s');
  });

  it('handles null input', () => {
    expect(formatETA(null)).toBe('-');
  });

  it('handles zero duration', () => {
    expect(formatETA('00:00:00')).toBe('-');
  });

  it('formats ETA with hours', () => {
    expect(formatETA('01:30:00')).toBe('~1h 30m');
  });
});
