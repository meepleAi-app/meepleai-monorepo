// apps/web/src/lib/play-records/draft-types.ts
import { z } from 'zod';

import type { PlayRecordVisibility } from '@/lib/api/schemas/play-records.schemas';

/** Bump to invalidate all persisted drafts on a breaking shape change. */
export const PLAY_RECORD_DRAFT_SCHEMA_VERSION = 1;

export interface PlayRecordDraftPlayer {
  id: string;
  name: string;
  score: string;
}

/** In-memory draft state (sessionDate as Date) — input to the persist hook. */
export interface PlayRecordDraftState {
  currentStep: number;
  gameType: 'catalog' | 'freeform';
  gameId?: string;
  gameName: string;
  sessionDate: Date;
  visibility: PlayRecordVisibility;
  enableScoring: boolean;
  scoringDimensions: string[];
  dimensionUnits: Record<string, string>;
  notes?: string;
  location?: string;
  players: PlayRecordDraftPlayer[];
}

/** Serialized draft persisted to localStorage (sessionDate as ISO string). */
export interface PersistedPlayRecordDraft {
  schemaVersion: number;
  currentStep: number;
  gameType: 'catalog' | 'freeform';
  gameId?: string;
  gameName: string;
  sessionDate: string;
  visibility: PlayRecordVisibility;
  enableScoring: boolean;
  scoringDimensions: string[];
  dimensionUnits: Record<string, string>;
  notes?: string;
  location?: string;
  players: PlayRecordDraftPlayer[];
}

const playerSchema = z.object({
  id: z.string(),
  name: z.string(),
  score: z.string(),
});

export const persistedPlayRecordDraftSchema = z.object({
  schemaVersion: z.literal(PLAY_RECORD_DRAFT_SCHEMA_VERSION),
  currentStep: z.number(),
  gameType: z.enum(['catalog', 'freeform']),
  gameId: z.string().optional(),
  gameName: z.string(),
  sessionDate: z.string(),
  visibility: z.string(),
  enableScoring: z.boolean(),
  scoringDimensions: z.array(z.string()),
  dimensionUnits: z.record(z.string(), z.string()),
  notes: z.string().optional(),
  location: z.string().optional(),
  players: z.array(playerSchema),
});
