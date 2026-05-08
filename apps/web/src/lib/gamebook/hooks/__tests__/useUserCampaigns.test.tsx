import { describe, expect, it } from 'vitest';

import { deriveIsStale } from '../useUserCampaigns';

describe('deriveIsStale', () => {
  const now = new Date('2026-05-08T12:00:00Z');

  it('returns false when lastReadAt is today', () => {
    expect(deriveIsStale('2026-05-08T08:00:00Z', now)).toBe(false);
  });

  it('returns false when lastReadAt is 89 days ago', () => {
    const last = new Date(now);
    last.setUTCDate(last.getUTCDate() - 89);
    expect(deriveIsStale(last.toISOString(), now)).toBe(false);
  });

  it('returns true when lastReadAt is exactly 90 days ago', () => {
    const last = new Date(now);
    last.setUTCDate(last.getUTCDate() - 90);
    expect(deriveIsStale(last.toISOString(), now)).toBe(true);
  });

  it('returns true when lastReadAt is 100 days ago', () => {
    const last = new Date(now);
    last.setUTCDate(last.getUTCDate() - 100);
    expect(deriveIsStale(last.toISOString(), now)).toBe(true);
  });

  it('returns false on invalid date string', () => {
    expect(deriveIsStale('not-a-date', now)).toBe(false);
  });

  it('honors a custom threshold', () => {
    const last = new Date(now);
    last.setUTCDate(last.getUTCDate() - 30);
    expect(deriveIsStale(last.toISOString(), now, 30)).toBe(true);
    expect(deriveIsStale(last.toISOString(), now, 31)).toBe(false);
  });
});
