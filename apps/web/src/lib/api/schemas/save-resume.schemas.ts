/**
 * Save/Resume API Schemas
 *
 * Zod schemas for Enhanced Save/Resume feature.
 * Maps to SessionSaveResultDto and SessionResumeContextDto.
 *
 * Issue #122 — Enhanced Save/Resume
 */

import { z } from 'zod';

// ========== Save Complete ==========

export const SessionSaveResultSchema = z.object({
  sessionId: z.string().uuid(),
  snapshotIndex: z.number().int(),
  recap: z.string(),
  photoCount: z.number().int(),
  savedAt: z.string(),
});

export type SessionSaveResult = z.infer<typeof SessionSaveResultSchema>;

// ========== Resume Context ==========

export const PlayerScoreSummarySchema = z.object({
  playerId: z.string().uuid(),
  name: z.string(),
  totalScore: z.number().int(),
  rank: z.number().int(),
});

export type PlayerScoreSummary = z.infer<typeof PlayerScoreSummarySchema>;

export const SessionPhotoSummarySchema = z.object({
  attachmentId: z.string().uuid(),
  thumbnailUrl: z.string().nullable(),
  caption: z.string().nullable(),
  attachmentType: z.string(),
});

export type SessionPhotoSummary = z.infer<typeof SessionPhotoSummarySchema>;

export const SessionResumeContextSchema = z.object({
  sessionId: z.string().uuid(),
  gameTitle: z.string(),
  lastSnapshotIndex: z.number().int(),
  currentTurn: z.number().int(),
  currentPhase: z.string().nullable(),
  pausedAt: z.string(),
  recap: z.string(),
  playerScores: z.array(PlayerScoreSummarySchema),
  photos: z.array(SessionPhotoSummarySchema),
});

export type SessionResumeContext = z.infer<typeof SessionResumeContextSchema>;
