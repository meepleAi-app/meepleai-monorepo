/**
 * formatRelativeDate — Task 0.2 (Issue #1488).
 *
 * Italian-locale aware relative date formatter for play-records.
 *
 * Buckets:
 *   - within 24h same calendar day → "oggi"
 *   - 1 calendar day ago           → "ieri"
 *   - 2..30 calendar days ago      → "N giorni fa" (formatDistanceToNowStrict locale=it)
 *   - future date                  → "tra N giorn{i,o}" (formatDistanceToNowStrict addSuffix)
 *   - more than 30 days ago        → short date "5 mag" (format(d, 'd MMM', { locale: it }))
 *   - null input                   → "—"
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { formatRelativeDate } from '../formatRelativeDate';

describe('formatRelativeDate', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    // Friday 2026-05-29 14:00 UTC
    vi.setSystemTime(new Date('2026-05-29T14:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('formats today (within 24h same calendar day) as "oggi"', () => {
    expect(formatRelativeDate('2026-05-29T10:00:00Z')).toBe('oggi');
  });

  it('formats yesterday as "ieri"', () => {
    expect(formatRelativeDate('2026-05-28T14:00:00Z')).toBe('ieri');
  });

  it('formats N days ago as "N giorni fa"', () => {
    expect(formatRelativeDate('2026-05-25T14:00:00Z')).toMatch(/4 giorni fa/);
  });

  it('formats future as "tra N giorni" (EC-7)', () => {
    expect(formatRelativeDate('2026-06-01T14:00:00Z')).toMatch(/tra 3 giorni/);
  });

  it('formats >30 days ago as short Italian date "15 apr"', () => {
    expect(formatRelativeDate('2026-04-15T14:00:00Z')).toBe('15 apr');
  });

  it('returns "—" for null input', () => {
    expect(formatRelativeDate(null)).toBe('—');
  });
});
