/**
 * Unit tests for formatLastReindex — #1676 scope (a) F2.
 *
 * Strategy: avoid time mocking — call `formatLastReindex` with concrete dates
 * relative to `new Date()` so the test is robust to the project's existing
 * `formatRelativeDate` (which uses `now()` at call-site).
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { formatLastReindex } from '../last-reindex';

const REFERENCE_NOW = new Date('2026-06-01T12:00:00.000Z');

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(REFERENCE_NOW);
});

afterEach(() => {
  vi.useRealTimers();
});

describe('formatLastReindex', () => {
  it('returns "📤 upload only" when lastIngestedAt equals uploadedAt', () => {
    const same = '2026-05-24T10:30:00.000Z';
    const result = formatLastReindex(same, same);
    expect(result.kind).toBe('upload-only');
    expect(result.label).toBe('📤 upload only');
  });

  it('renders "🔄 last reindex {relative}" for distinct timestamps', () => {
    // 8 calendar days before reference now
    const eightDaysAgo = '2026-05-24T12:00:00.000Z';
    const uploaded = '2026-05-01T12:00:00.000Z'; // 31d before
    const result = formatLastReindex(eightDaysAgo, uploaded);
    expect(result.kind).toBe('reindex');
    expect(result.label.startsWith('🔄 last reindex ')).toBe(true);
    // The exact relative-date string is owned by formatRelativeDate; just
    // assert it's non-empty and not the em-dash placeholder.
    expect(result.label).not.toContain('—');
    expect(result.label.length).toBeGreaterThan('🔄 last reindex '.length);
  });

  it('returns "—" when lastIngestedAt is null/undefined/invalid', () => {
    expect(formatLastReindex(null, '2026-05-01T12:00:00.000Z').label).toBe('—');
    expect(formatLastReindex(undefined, '2026-05-01T12:00:00.000Z').label).toBe('—');
    expect(formatLastReindex('not-a-date', '2026-05-01T12:00:00.000Z').label).toBe('—');
  });

  it('renders reindex label when uploadedAt is null even if lastIngestedAt is valid', () => {
    // No upload-only check possible without uploadedAt → fall through to reindex.
    const result = formatLastReindex('2026-05-24T12:00:00.000Z', null);
    expect(result.kind).toBe('reindex');
    expect(result.label.startsWith('🔄 last reindex ')).toBe(true);
  });
});
