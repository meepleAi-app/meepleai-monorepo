import { describe, expect, it } from 'vitest';
import { formatDuration, formatRelativeTime } from './run-formatter';

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
