/**
 * Session Invite Schemas (Game Night Improvvisata)
 *
 * Zod schemas for session invite, join, participants, and score proposal/confirm.
 */

import { z } from 'zod';

// ──────────────────────────────────────────────
// Request Schemas
// ──────────────────────────────────────────────

export const CreateInviteRequestSchema = z.object({
  maxUses: z.number().int().positive().optional(),
  expiryMinutes: z.number().int().positive().optional(),
});

export type CreateInviteRequest = z.infer<typeof CreateInviteRequestSchema>;

export const JoinSessionRequestSchema = z.object({
  token: z.string().min(1),
  guestName: z.string().optional(),
});

export type JoinSessionRequest = z.infer<typeof JoinSessionRequestSchema>;

export const ToggleAgentAccessRequestSchema = z.object({
  enabled: z.boolean(),
});

export type ToggleAgentAccessRequest = z.infer<typeof ToggleAgentAccessRequestSchema>;

export const ProposeScoreProposalRequestSchema = z.object({
  participantId: z.string().uuid(),
  targetPlayerId: z.string().uuid(),
  round: z.number().int(),
  dimension: z.string().min(1),
  value: z.number(),
  proposerName: z.string().optional(),
});

export type ProposeScoreProposalRequest = z.infer<typeof ProposeScoreProposalRequestSchema>;

export const ConfirmScoreProposalRequestSchema = z.object({
  targetPlayerId: z.string().uuid(),
  round: z.number().int(),
  dimension: z.string().min(1),
  value: z.number(),
});

export type ConfirmScoreProposalRequest = z.infer<typeof ConfirmScoreProposalRequestSchema>;

// ──────────────────────────────────────────────
// Response Schemas
// ──────────────────────────────────────────────

export const SessionInviteResultSchema = z.object({
  pin: z.string(),
  linkToken: z.string(),
  expiresAt: z.string(),
});

export type SessionInviteResult = z.infer<typeof SessionInviteResultSchema>;

export const JoinSessionResultSchema = z.object({
  sessionId: z.string().uuid(),
  participantId: z.string().uuid(),
  connectionToken: z.string(),
  displayName: z.string(),
  role: z.string(),
});

export type JoinSessionResult = z.infer<typeof JoinSessionResultSchema>;

export const SessionParticipantDtoSchema = z.object({
  id: z.string().uuid(),
  sessionId: z.string().uuid(),
  userId: z.string().uuid().nullable(),
  guestName: z.string().nullable().optional(),
  displayName: z.string(),
  role: z.string(),
  agentAccessEnabled: z.boolean(),
  joinedAt: z.string(),
  leftAt: z.string().nullable().optional(),
});

export type SessionParticipantDto = z.infer<typeof SessionParticipantDtoSchema>;
