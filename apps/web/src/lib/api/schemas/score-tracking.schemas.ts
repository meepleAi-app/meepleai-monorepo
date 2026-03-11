/**
 * Score Tracking AI Schemas
 *
 * Zod schemas for AI-mediated score parsing and confirmation.
 * Maps to ParseAndRecordScoreCommand / ConfirmScoreCommand in KnowledgeBase BC.
 *
 * Issue #121 — AI Score Tracking
 */

import { z } from 'zod';

// ========== Request Schemas ==========

export const ParseScoreRequestSchema = z.object({
  message: z.string().min(1).max(500),
  autoRecord: z.boolean().optional().default(true),
});

export type ParseScoreRequest = z.infer<typeof ParseScoreRequestSchema>;

export const ConfirmScoreRequestSchema = z.object({
  playerId: z.string().uuid(),
  dimension: z.string().min(1),
  value: z.number().int(),
  round: z.number().int().min(1),
});

export type ConfirmScoreRequest = z.infer<typeof ConfirmScoreRequestSchema>;

// ========== Response Schemas ==========

export const ScoreParseResultSchema = z.object({
  status: z.enum(['parsed', 'recorded', 'ambiguous', 'unrecognized']),
  playerName: z.string().nullish(),
  playerId: z.string().uuid().nullish(),
  dimension: z.string().nullish(),
  value: z.number().int().nullish(),
  round: z.number().int().nullish(),
  confidence: z.number().min(0).max(1),
  requiresConfirmation: z.boolean(),
  message: z.string(),
  ambiguousCandidates: z.array(z.string()).nullish(),
});

export type ScoreParseResult = z.infer<typeof ScoreParseResultSchema>;
