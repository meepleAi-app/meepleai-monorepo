/**
 * Play Records API Schemas
 *
 * Zod schemas for validating Play Records API responses and requests.
 * Issue #3892: Frontend UI + Statistics Dashboard
 *
 * Backend: GameManagement bounded context (Issues #3888-3891)
 */

import { z } from 'zod';

// ========== Enums ==========

export const PlayRecordStatusSchema = z.enum(['Planned', 'InProgress', 'Completed', 'Archived']);
export type PlayRecordStatus = z.infer<typeof PlayRecordStatusSchema>;

export const PlayRecordVisibilitySchema = z.enum(['Private', 'Group']);
export type PlayRecordVisibility = z.infer<typeof PlayRecordVisibilitySchema>;

// ========== Value Objects ==========

export const SessionScoreSchema = z.object({
  dimension: z.string().min(1),
  value: z.number().int(),
  unit: z.string().nullable(),
});
export type SessionScore = z.infer<typeof SessionScoreSchema>;

export const SessionScoringConfigSchema = z.object({
  enabledDimensions: z.array(z.string()),
  dimensionUnits: z.record(z.string(), z.string()),
});
export type SessionScoringConfig = z.infer<typeof SessionScoringConfigSchema>;

export const SessionPlayerSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid().nullable(),
  displayName: z.string(),
  scores: z.array(SessionScoreSchema),
});
export type SessionPlayer = z.infer<typeof SessionPlayerSchema>;

// ========== DTOs ==========

export const PlayRecordDtoSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid().nullable(),
  gameName: z.string(),
  sessionDate: z.string(),
  duration: z.string().nullable(), // .NET TimeSpan (e.g., "02:30:00") or ISO 8601 (e.g., "PT2H30M")
  status: PlayRecordStatusSchema,
  players: z.array(SessionPlayerSchema),
  scoringConfig: SessionScoringConfigSchema,
  createdByUserId: z.string().uuid(),
  visibility: PlayRecordVisibilitySchema,
  startTime: z.string().nullable(),
  endTime: z.string().nullable(),
  notes: z.string().nullable(),
  location: z.string().nullable(),
  createdAt: z.string(),
  updatedAt: z.string(),
});
export type PlayRecordDto = z.infer<typeof PlayRecordDtoSchema>;

export const PlayRecordSummarySchema = z.object({
  id: z.string().uuid(),
  gameName: z.string(),
  sessionDate: z.string(),
  duration: z.string().nullable(),
  status: PlayRecordStatusSchema,
  playerCount: z.number().int().nonnegative(),
});
export type PlayRecordSummary = z.infer<typeof PlayRecordSummarySchema>;

export const PlayerStatisticsSchema = z.object({
  totalSessions: z.number().int().nonnegative(),
  totalWins: z.number().int().nonnegative(),
  gamePlayCounts: z.record(z.string(), z.number().int()),
  averageScoresByGame: z.record(z.string(), z.number()),
});
export type PlayerStatistics = z.infer<typeof PlayerStatisticsSchema>;

// ========== Request DTOs ==========

export const CreatePlayRecordRequestSchema = z.object({
  gameId: z.string().uuid().optional(),
  gameName: z.string().min(1).max(255),
  sessionDate: z.string(),
  visibility: PlayRecordVisibilitySchema,
  groupId: z.string().uuid().optional(),
  scoringDimensions: z.array(z.string()).optional(),
  dimensionUnits: z.record(z.string(), z.string()).optional(),
});
export type CreatePlayRecordRequest = z.infer<typeof CreatePlayRecordRequestSchema>;

export const AddPlayerRequestSchema = z.object({
  userId: z.string().uuid().optional(),
  displayName: z.string().min(1).max(50),
});
export type AddPlayerRequest = z.infer<typeof AddPlayerRequestSchema>;

export const RecordScoreRequestSchema = z.object({
  playerId: z.string().uuid(),
  dimension: z.string().min(1),
  value: z.number().int(),
  unit: z.string().optional(),
});
export type RecordScoreRequest = z.infer<typeof RecordScoreRequestSchema>;

export const UpdatePlayRecordRequestSchema = z.object({
  sessionDate: z.string().optional(),
  notes: z.string().max(2000).optional(),
  location: z.string().max(255).optional(),
});
export type UpdatePlayRecordRequest = z.infer<typeof UpdatePlayRecordRequestSchema>;

// ========== Response DTOs ==========

export const PlayHistoryResponseSchema = z.object({
  records: z.array(PlayRecordSummarySchema),
  totalCount: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
  totalPages: z.number().int().nonnegative(),
});
export type PlayHistoryResponse = z.infer<typeof PlayHistoryResponseSchema>;

// ========== Form Schemas (Client-side validation) ==========

export const SessionCreateFormSchema = z
  .object({
    gameType: z.enum(['catalog', 'freeform']),
    gameId: z.string().uuid().optional(),
    gameName: z.string().min(1, 'Game name is required').max(255),
    sessionDate: z.date(),
    visibility: PlayRecordVisibilitySchema,
    groupId: z.string().uuid().optional(),
    enableScoring: z.boolean().optional(),
    scoringDimensions: z.array(z.string()).optional(),
    dimensionUnits: z.record(z.string(), z.string()).optional(),
    notes: z.string().max(2000).optional(),
    location: z.string().max(255).optional(),
  })
  .refine(
    data => {
      // If catalog game selected, gameId must be provided
      if (data.gameType === 'catalog' && !data.gameId) return false;
      // If Group visibility, groupId must be provided
      if (data.visibility === 'Group' && !data.groupId) return false;
      return true;
    },
    {
      message: 'Invalid game or group selection',
    }
  );
export type SessionCreateForm = z.infer<typeof SessionCreateFormSchema>;

export const PlayerAddFormSchema = z
  .object({
    playerType: z.enum(['user', 'guest']),
    userId: z.string().uuid().optional(),
    displayName: z.string().min(1, 'Name is required').max(50),
  })
  .refine(
    data => {
      // If user type, userId must be provided
      if (data.playerType === 'user' && !data.userId) return false;
      return true;
    },
    {
      message: 'User selection required for registered players',
    }
  );
export type PlayerAddForm = z.infer<typeof PlayerAddFormSchema>;

export const ScoreInputFormSchema = z.object({
  scores: z.array(
    z.object({
      playerId: z.string().uuid(),
      dimension: z.string(),
      value: z.number().int().nonnegative('Score must be non-negative'),
      unit: z.string().optional(),
    })
  ),
});
export type ScoreInputForm = z.infer<typeof ScoreInputFormSchema>;
