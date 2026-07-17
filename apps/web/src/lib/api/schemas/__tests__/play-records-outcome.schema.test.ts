import { describe, it, expect } from 'vitest';

import { PlayRecordDtoSchema, PlayRecordSummarySchema } from '../play-records.schemas';

// #3087: winnerPlayerIds + outcomeType are now REQUIRED. The BE always emits them —
// they are non-nullable positional params on PlayRecordDto/PlayRecordSummaryDto, populated
// unconditionally by PlayRecordDtoMapper via PlayRecordOutcomeCalculator. A response that
// dropped either field must fail fast, not parse to `undefined`.

const baseDto = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  gameId: null,
  gameName: 'Catan',
  sessionDate: '2026-07-17',
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
  createdAt: '2026-07-17T10:00:00Z',
  updatedAt: '2026-07-17T10:00:00Z',
  winnerPlayerIds: ['550e8400-e29b-41d4-a716-446655440002'],
  outcomeType: 'competitive' as const,
};

const baseSummary = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  gameName: 'Catan',
  sessionDate: '2026-07-17',
  duration: null,
  status: 'Completed' as const,
  playerCount: 2,
  gameId: null,
  winnerPlayerIds: ['550e8400-e29b-41d4-a716-446655440002'],
  outcomeType: 'competitive' as const,
};

describe('PlayRecordDtoSchema outcome fields (#3087)', () => {
  it('parses a record carrying winnerPlayerIds + outcomeType', () => {
    const parsed = PlayRecordDtoSchema.parse(baseDto);
    expect(parsed.winnerPlayerIds).toEqual(['550e8400-e29b-41d4-a716-446655440002']);
    expect(parsed.outcomeType).toBe('competitive');
  });

  it('rejects a record missing winnerPlayerIds', () => {
    const invalid: Partial<typeof baseDto> = { ...baseDto };
    delete invalid.winnerPlayerIds;
    expect(() => PlayRecordDtoSchema.parse(invalid)).toThrow();
  });

  it('rejects a record missing outcomeType', () => {
    const invalid: Partial<typeof baseDto> = { ...baseDto };
    delete invalid.outcomeType;
    expect(() => PlayRecordDtoSchema.parse(invalid)).toThrow();
  });
});

describe('PlayRecordSummarySchema outcome fields (#3087)', () => {
  it('parses a summary carrying gameId + winnerPlayerIds + outcomeType', () => {
    const parsed = PlayRecordSummarySchema.parse(baseSummary);
    expect(parsed.outcomeType).toBe('competitive');
    expect(parsed.gameId).toBeNull();
  });

  it('rejects a summary missing winnerPlayerIds', () => {
    const invalid: Partial<typeof baseSummary> = { ...baseSummary };
    delete invalid.winnerPlayerIds;
    expect(() => PlayRecordSummarySchema.parse(invalid)).toThrow();
  });

  it('rejects a summary missing outcomeType', () => {
    const invalid: Partial<typeof baseSummary> = { ...baseSummary };
    delete invalid.outcomeType;
    expect(() => PlayRecordSummarySchema.parse(invalid)).toThrow();
  });
});
