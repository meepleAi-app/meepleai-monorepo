/**
 * history-format — unit tests (Issue #3010, Task 5).
 *
 * Covers the boundary cases the four call sites (HistoryTable, HistoryCards,
 * HistoryDetailModal, client.tsx) relied on before dedup: zero duration,
 * multi-hour duration, single-word / multi-word / empty / multi-space names,
 * and the shared avatar-collapse constant.
 */

import { describe, expect, it } from 'vitest';

import { formatDuration, getInitials, MAX_AVATARS } from '../history-format';

describe('formatDuration', () => {
  it('formats 0 minutes as "0h 0m"', () => {
    expect(formatDuration(0)).toBe('0h 0m');
  });

  it('formats 102 minutes as "1h 42m"', () => {
    expect(formatDuration(102)).toBe('1h 42m');
  });
});

describe('getInitials', () => {
  it('returns first-letter-of-first-and-last-word initials for a two-word name', () => {
    expect(getInitials('Marco Rossi')).toBe('MR');
  });

  it('returns the first 2 letters uppercased for a single-word name', () => {
    expect(getInitials('Ada')).toBe('AD');
  });

  it('returns "?" for an empty name', () => {
    expect(getInitials('')).toBe('?');
  });

  it('handles extra internal/leading/trailing whitespace across 3+ words', () => {
    expect(getInitials('  a  b  c ')).toBe('AC');
  });
});

describe('MAX_AVATARS', () => {
  it('is 3', () => {
    expect(MAX_AVATARS).toBe(3);
  });
});
