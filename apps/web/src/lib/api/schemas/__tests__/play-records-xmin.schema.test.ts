import { describe, it, expect } from 'vitest';

import { PlayRecordDtoSchema, UpdatePlayRecordRequestSchema } from '../play-records.schemas';

const base = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  gameId: null,
  gameName: 'Catan',
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

describe('xmin schemas (#2437-1)', () => {
  it('PlayRecordDtoSchema accepts a numeric xmin and is optional', () => {
    expect(PlayRecordDtoSchema.parse(base).xmin).toBeUndefined();
    expect(PlayRecordDtoSchema.parse({ ...base, xmin: 4242 }).xmin).toBe(4242);
  });
  it('UpdatePlayRecordRequestSchema accepts optional xmin', () => {
    expect(UpdatePlayRecordRequestSchema.parse({ notes: 'x' }).xmin).toBeUndefined();
    expect(UpdatePlayRecordRequestSchema.parse({ notes: 'x', xmin: 7 }).xmin).toBe(7);
  });
});
