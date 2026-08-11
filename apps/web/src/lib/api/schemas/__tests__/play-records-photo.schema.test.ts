import { describe, it, expect } from 'vitest';

import { PlayRecordDtoSchema, PlayRecordPhotoSchema } from '../play-records.schemas';

describe('PlayRecordPhotoSchema', () => {
  it('parses a full photo DTO', () => {
    const parsed = PlayRecordPhotoSchema.parse({
      id: '550e8400-e29b-41d4-a716-446655440001',
      url: 'https://cdn.example/p.webp',
      thumbnailUrl: 'https://cdn.example/t.webp',
      ocrText: '42',
      caption: 'scoreboard',
      uploadedByUserId: '550e8400-e29b-41d4-a716-446655440002',
      uploadedAt: '2026-06-20T10:00:00Z',
    });
    expect(parsed.url).toBe('https://cdn.example/p.webp');
  });

  it('accepts null thumbnail/ocr/caption', () => {
    const parsed = PlayRecordPhotoSchema.parse({
      id: '550e8400-e29b-41d4-a716-446655440001',
      url: 'https://cdn.example/p.webp',
      thumbnailUrl: null,
      ocrText: null,
      caption: null,
      uploadedByUserId: '550e8400-e29b-41d4-a716-446655440002',
      uploadedAt: '2026-06-20T10:00:00Z',
    });
    expect(parsed.thumbnailUrl).toBeNull();
  });

  it('PlayRecordDtoSchema treats photos as optional (BE rollout)', () => {
    const base = {
      id: '550e8400-e29b-41d4-a716-446655440003',
      gameId: null,
      gameName: 'Catan',
      sessionDate: '2026-06-20',
      duration: null,
      status: 'Completed' as const,
      players: [],
      scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
      createdByUserId: '550e8400-e29b-41d4-a716-446655440002',
      visibility: 'Private' as const,
      startTime: null,
      endTime: null,
      notes: null,
      location: null,
      createdAt: '2026-06-20T10:00:00Z',
      updatedAt: '2026-06-20T10:00:00Z',
      winnerPlayerIds: [],
      outcomeType: 'none' as const,
    };
    expect(PlayRecordDtoSchema.parse(base).photos).toBeUndefined();
    expect(PlayRecordDtoSchema.parse({ ...base, photos: [] }).photos).toEqual([]);
  });
});
