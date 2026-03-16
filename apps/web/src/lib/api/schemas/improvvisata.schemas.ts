/**
 * Improvvisata API Schemas
 *
 * Game Night Improvvisata — Task 14
 *
 * Zod schemas for the /api/v1/game-night/* endpoints:
 *   - Start improvvisata session
 *   - Rule dispute submission and retrieval
 *   - Pause snapshot and resume
 */

import { z } from 'zod';

// ──────────────────────────────────────────────
// Start Improvvisata Session
// ──────────────────────────────────────────────

export const StartImprovvisataResponseSchema = z.object({
  sessionId: z.string().uuid(),
  inviteCode: z.string(),
  shareLink: z.string(),
});

export type StartImprovvisataResponse = z.infer<typeof StartImprovvisataResponseSchema>;

// ──────────────────────────────────────────────
// Rule Dispute
// ──────────────────────────────────────────────

export const RuleDisputeResponseSchema = z.object({
  id: z.string().uuid(),
  verdict: z.string(),
  ruleReferences: z.array(z.string()),
  note: z.string().nullable(),
});

export type RuleDisputeResponse = z.infer<typeof RuleDisputeResponseSchema>;

export const RuleDisputeDtoSchema = z.object({
  id: z.string().uuid(),
  description: z.string(),
  verdict: z.string(),
  ruleReferences: z.array(z.string()),
  raisedByPlayerName: z.string(),
  timestamp: z.string(),
});

export type RuleDisputeDto = z.infer<typeof RuleDisputeDtoSchema>;

export const GameDisputeHistoryItemSchema = z.object({
  sessionId: z.string().uuid(),
  disputes: z.array(RuleDisputeDtoSchema),
});

export type GameDisputeHistoryItem = z.infer<typeof GameDisputeHistoryItemSchema>;

// ──────────────────────────────────────────────
// Pause Snapshot
// ──────────────────────────────────────────────

export const CreatePauseSnapshotResponseSchema = z.object({
  snapshotId: z.string().uuid(),
});

export type CreatePauseSnapshotResponse = z.infer<typeof CreatePauseSnapshotResponseSchema>;

// ──────────────────────────────────────────────
// Resume Session
// ──────────────────────────────────────────────

export const ResumeSessionResponseSchema = z.object({
  sessionId: z.string().uuid(),
  inviteCode: z.string(),
  shareLink: z.string(),
  agentRecap: z.string().nullable(),
});

export type ResumeSessionResponse = z.infer<typeof ResumeSessionResponseSchema>;
