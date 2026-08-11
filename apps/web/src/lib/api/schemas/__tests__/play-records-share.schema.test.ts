import { describe, it, expect } from 'vitest';

import { ShareLinkResponseSchema, PlayRecordDtoSchema } from '../play-records.schemas';

// Minimal valid PlayRecordDto base (no optional fields)
const basePr = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  gameId: null,
  gameName: 'Wingspan',
  sessionDate: '2026-06-21',
  duration: null,
  status: 'Completed' as const,
  players: [],
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
  createdByUserId: '550e8400-e29b-41d4-a716-446655440001',
  visibility: 'Private' as const,
  startTime: null,
  endTime: null,
  notes: null,
  location: null,
  createdAt: '2026-06-21T10:00:00Z',
  updatedAt: '2026-06-21T10:00:00Z',
  winnerPlayerIds: [],
  outcomeType: 'none' as const,
};

describe('ShareLinkResponseSchema (#2437-2)', () => {
  it('parses a valid share link response', () => {
    const result = ShareLinkResponseSchema.parse({
      shareToken: 'abc123token',
      shareUrl: 'https://app.meeple.ai/shared/abc123token',
    });
    expect(result.shareToken).toBe('abc123token');
    expect(result.shareUrl).toBe('https://app.meeple.ai/shared/abc123token');
  });

  it('rejects when shareToken is missing', () => {
    expect(() =>
      ShareLinkResponseSchema.parse({ shareUrl: 'https://app.meeple.ai/shared/tok' })
    ).toThrow();
  });

  it('rejects when shareUrl is missing', () => {
    expect(() => ShareLinkResponseSchema.parse({ shareToken: 'tok' })).toThrow();
  });
});

describe('PlayRecordDtoSchema.shareToken (#2437-2)', () => {
  it('accepts a DTO without shareToken (field is optional)', () => {
    const result = PlayRecordDtoSchema.parse(basePr);
    expect(result.shareToken).toBeUndefined();
  });

  it('accepts shareToken as a non-null string', () => {
    const result = PlayRecordDtoSchema.parse({ ...basePr, shareToken: 'mytoken' });
    expect(result.shareToken).toBe('mytoken');
  });

  it('accepts shareToken as null (not shared)', () => {
    const result = PlayRecordDtoSchema.parse({ ...basePr, shareToken: null });
    expect(result.shareToken).toBeNull();
  });
});
