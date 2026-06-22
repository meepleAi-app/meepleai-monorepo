// apps/web/src/lib/play-records/draft-types.test.ts
import { describe, it, expect } from 'vitest';

import {
  PLAY_RECORD_DRAFT_SCHEMA_VERSION,
  persistedPlayRecordDraftSchema,
  type PersistedPlayRecordDraft,
} from './draft-types';

function validDraft(): PersistedPlayRecordDraft {
  return {
    schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
    currentStep: 1,
    gameType: 'catalog',
    gameId: 'game-1',
    gameName: 'Wingspan',
    sessionDate: '2026-06-20T18:00:00.000Z',
    visibility: 'Private',
    enableScoring: false,
    scoringDimensions: [],
    dimensionUnits: {},
    notes: 'gg',
    location: 'Padova',
    players: [{ id: 'p1', name: 'Marco', score: '42' }],
  };
}

describe('persistedPlayRecordDraftSchema', () => {
  it('accepts a well-formed draft of the current schema version', () => {
    const result = persistedPlayRecordDraftSchema.safeParse(validDraft());
    expect(result.success).toBe(true);
  });

  it('rejects a draft whose schemaVersion does not match (version bump guard)', () => {
    const stale = { ...validDraft(), schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION + 1 };
    expect(persistedPlayRecordDraftSchema.safeParse(stale).success).toBe(false);
  });

  it('rejects a corrupted payload (missing required field)', () => {
    const broken = { ...validDraft() } as Record<string, unknown>;
    delete broken.players;
    expect(persistedPlayRecordDraftSchema.safeParse(broken).success).toBe(false);
  });
});
