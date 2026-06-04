import { describe, expect, it } from 'vitest';
import { formatDuration, formatRelativeTime, parseTimeSpanToMs } from './run-formatter';

describe('formatDuration', () => {
  it('formats milliseconds under 1s', () => {
    expect(formatDuration(250)).toBe('250ms');
  });
  it('formats seconds under 1m with 1 decimal', () => {
    expect(formatDuration(2400)).toBe('2.4s');
  });
  it('formats minutes + seconds pad', () => {
    expect(formatDuration(258000)).toBe('4m 18s');
    expect(formatDuration(232000)).toBe('3m 52s');
    expect(formatDuration(362000)).toBe('6m 02s'); // pad to 2 digits
  });
});

describe('formatRelativeTime', () => {
  const now = new Date('2026-06-04T14:22:00Z');

  it('returns "Xs fa" under 60s', () => {
    expect(formatRelativeTime('2026-06-04T14:21:30Z', now)).toBe('30s fa');
  });
  it('returns "X min fa" under 1h', () => {
    expect(formatRelativeTime('2026-06-04T14:08:00Z', now)).toBe('14 min fa');
  });
  it('returns "Xh fa" under 24h', () => {
    expect(formatRelativeTime('2026-06-04T08:22:00Z', now)).toBe('6h fa');
  });
  it('returns "Xgg fa" beyond 24h', () => {
    expect(formatRelativeTime('2026-06-01T14:22:00Z', now)).toBe('3gg fa');
  });
});

describe('parseTimeSpanToMs', () => {
  it('returns null for null input', () => {
    expect(parseTimeSpanToMs(null)).toBeNull();
  });

  it('parses "00:00:01" as 1000ms', () => {
    expect(parseTimeSpanToMs('00:00:01')).toBe(1000);
  });

  it('parses "00:04:18" as 258000ms (4m 18s)', () => {
    expect(parseTimeSpanToMs('00:04:18')).toBe(258000);
  });

  it('parses "01:02:03" as 3723000ms (1h 2m 3s)', () => {
    expect(parseTimeSpanToMs('01:02:03')).toBe(3723000);
  });

  it('parses fractional seconds "00:00:00.5" as 500ms', () => {
    expect(parseTimeSpanToMs('00:00:00.5')).toBe(500);
  });

  it('parses day-prefix "1.02:00:00" as 93600000ms (1d 2h)', () => {
    expect(parseTimeSpanToMs('1.02:00:00')).toBe(93600000);
  });

  it('returns null for malformed input', () => {
    expect(parseTimeSpanToMs('not-a-timespan')).toBeNull();
    expect(parseTimeSpanToMs('00:99')).toBeNull(); // only 2 segments
  });
});
