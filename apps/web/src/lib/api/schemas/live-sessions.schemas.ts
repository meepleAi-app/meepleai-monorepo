/**
 * Live Sessions API Schemas
 *
 * Zod schemas for LiveGameSession bounded context (GameManagement).
 * Maps to LiveSessionEndpoints.cs DTOs.
 *
 * Issue #5041 — Sessions Redesign Phase 1
 */

import { z } from 'zod';

// ========== Enums ==========

export const LiveSessionStatusSchema = z.enum([
  'Created',
  'Setup',
  'InProgress',
  'Paused',
  'Completed',
]);

export type LiveSessionStatus = z.infer<typeof LiveSessionStatusSchema>;

export const PlayRecordVisibilitySchema = z.enum(['Private', 'Public', 'Group']);

export type PlayRecordVisibility = z.infer<typeof PlayRecordVisibilitySchema>;

export const AgentSessionModeSchema = z.enum(['None', 'Passive', 'Active', 'Moderator']);

export type AgentSessionMode = z.infer<typeof AgentSessionModeSchema>;

export const PlayerColorSchema = z.enum([
  'Red',
  'Blue',
  'Green',
  'Yellow',
  'Purple',
  'Orange',
  'White',
  'Black',
  'Pink',
  'Teal',
]);

export type PlayerColor = z.infer<typeof PlayerColorSchema>;

export const PlayerRoleSchema = z.enum(['Player', 'Spectator', 'Moderator', 'Host']);

export type PlayerRole = z.infer<typeof PlayerRoleSchema>;

export const SnapshotTriggerSchema = z.enum([
  'Manual',
  'TurnEnd',
  'PhaseEnd',
  'RoundEnd',
  'Checkpoint',
]);

export type SnapshotTrigger = z.infer<typeof SnapshotTriggerSchema>;

export const BaseToolTypeSchema = z.enum(['TurnOrder', 'DiceSet', 'Whiteboard', 'Scoreboard']);

export type BaseToolType = z.infer<typeof BaseToolTypeSchema>;

// ========== Response DTOs ==========

export const LiveSessionPlayerDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid().nullable(),
  displayName: z.string(),
  avatarUrl: z.string().nullable(),
  color: PlayerColorSchema,
  role: PlayerRoleSchema,
  teamId: z.string().uuid().nullable(),
  totalScore: z.number().int(),
  currentRank: z.number().int(),
  joinedAt: z.string(),
  isActive: z.boolean(),
});

export type LiveSessionPlayerDto = z.infer<typeof LiveSessionPlayerDtoSchema>;

export const LiveSessionTeamDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  color: z.string(),
  playerIds: z.array(z.string().uuid()),
  teamScore: z.number().int(),
  currentRank: z.number().int(),
});

export type LiveSessionTeamDto = z.infer<typeof LiveSessionTeamDtoSchema>;

export const LiveSessionRoundScoreDtoSchema = z.object({
  playerId: z.string().uuid(),
  round: z.number().int(),
  dimension: z.string(),
  value: z.number().int(),
  unit: z.string().nullable(),
  recordedAt: z.string(),
});

export type LiveSessionRoundScoreDto = z.infer<typeof LiveSessionRoundScoreDtoSchema>;

export const LiveSessionScoringConfigDtoSchema = z.object({
  enabledDimensions: z.array(z.string()),
  dimensionUnits: z.record(z.string(), z.string()),
});

export type LiveSessionScoringConfigDto = z.infer<typeof LiveSessionScoringConfigDtoSchema>;

export const LiveSessionDtoSchema = z.object({
  id: z.string().uuid(),
  sessionCode: z.string(),
  gameId: z.string().uuid().nullable(),
  gameName: z.string(),
  createdByUserId: z.string().uuid(),
  status: LiveSessionStatusSchema,
  visibility: PlayRecordVisibilitySchema,
  groupId: z.string().uuid().nullable(),
  createdAt: z.string(),
  startedAt: z.string().nullable(),
  pausedAt: z.string().nullable(),
  completedAt: z.string().nullable(),
  updatedAt: z.string(),
  lastSavedAt: z.string().nullable(),
  currentTurnIndex: z.number().int(),
  currentTurnPlayerId: z.string().uuid().nullable(),
  agentMode: AgentSessionModeSchema,
  chatSessionId: z.string().uuid().nullable(),
  notes: z.string().nullable(),
  players: z.array(LiveSessionPlayerDtoSchema),
  teams: z.array(LiveSessionTeamDtoSchema),
  roundScores: z.array(LiveSessionRoundScoreDtoSchema),
  scoringConfig: LiveSessionScoringConfigDtoSchema,
});

export type LiveSessionDto = z.infer<typeof LiveSessionDtoSchema>;

export const LiveSessionSummaryDtoSchema = z.object({
  id: z.string().uuid(),
  sessionCode: z.string(),
  gameName: z.string(),
  status: LiveSessionStatusSchema,
  playerCount: z.number().int(),
  currentTurnIndex: z.number().int(),
  createdAt: z.string(),
  updatedAt: z.string(),
  lastSavedAt: z.string().nullable(),
});

export type LiveSessionSummaryDto = z.infer<typeof LiveSessionSummaryDtoSchema>;

export const SessionSnapshotDtoSchema = z.object({
  id: z.string().uuid(),
  sessionId: z.string().uuid(),
  snapshotIndex: z.number().int(),
  triggerType: SnapshotTriggerSchema,
  triggerDescription: z.string().nullable(),
  isCheckpoint: z.boolean(),
  turnIndex: z.number().int(),
  phaseIndex: z.number().int().nullable(),
  timestamp: z.string(),
  createdByPlayerId: z.string().uuid().nullable(),
});

export type SessionSnapshotDto = z.infer<typeof SessionSnapshotDtoSchema>;

export const BaseToolDtoSchema = z.object({
  toolId: z.string(),
  name: z.string(),
  toolType: BaseToolTypeSchema,
  isAvailable: z.boolean(),
  defaultConfig: z.unknown(),
});

export type BaseToolDto = z.infer<typeof BaseToolDtoSchema>;

export const BaseToolkitDtoSchema = z.object({
  turnOrder: BaseToolDtoSchema,
  diceSet: BaseToolDtoSchema,
  whiteboard: BaseToolDtoSchema,
  scoreboard: BaseToolDtoSchema,
});

export type BaseToolkitDto = z.infer<typeof BaseToolkitDtoSchema>;

export const ToolStateDtoSchema = z.object({
  id: z.string().uuid(),
  sessionId: z.string().uuid(),
  toolkitId: z.string().uuid(),
  toolName: z.string(),
  toolType: z.string(),
  stateData: z.unknown(),
  createdAt: z.string(),
  lastUpdatedAt: z.string(),
});

export type ToolStateDto = z.infer<typeof ToolStateDtoSchema>;

export const SessionToolsDtoSchema = z.object({
  sessionId: z.string().uuid(),
  toolkitId: z.string().uuid().nullable(),
  baseTools: BaseToolkitDtoSchema,
  customTools: z.array(ToolStateDtoSchema),
});

export type SessionToolsDto = z.infer<typeof SessionToolsDtoSchema>;

export const TurnPhasesDtoSchema = z.object({
  currentPhaseIndex: z.number().int(),
  phaseNames: z.array(z.string()),
});

export type TurnPhasesDto = z.infer<typeof TurnPhasesDtoSchema>;

// ========== Request DTOs ==========

export const CreateLiveSessionRequestSchema = z.object({
  gameName: z.string().min(1).optional(),
  gameId: z.string().uuid().optional(),
  visibility: PlayRecordVisibilitySchema.optional(),
  groupId: z.string().uuid().optional(),
  scoringDimensions: z.array(z.string()).optional(),
  dimensionUnits: z.record(z.string(), z.string()).optional(),
  agentMode: AgentSessionModeSchema.optional(),
});

export type CreateLiveSessionRequest = z.infer<typeof CreateLiveSessionRequestSchema>;

export const AddPlayerRequestSchema = z.object({
  displayName: z.string().min(1),
  color: PlayerColorSchema.optional(),
  userId: z.string().uuid().optional(),
  role: PlayerRoleSchema.optional(),
  avatarUrl: z.string().url().optional(),
});

export type AddPlayerRequest = z.infer<typeof AddPlayerRequestSchema>;

export const UpdateTurnOrderRequestSchema = z.object({
  playerIds: z.array(z.string().uuid()),
});

export type UpdateTurnOrderRequest = z.infer<typeof UpdateTurnOrderRequestSchema>;

export const CreateTeamRequestSchema = z.object({
  name: z.string().min(1),
  color: z.string(),
});

export type CreateTeamRequest = z.infer<typeof CreateTeamRequestSchema>;

export const RecordScoreRequestSchema = z.object({
  playerId: z.string().uuid(),
  round: z.number().int().min(1),
  dimension: z.string().min(1),
  value: z.number().int(),
  unit: z.string().optional(),
});

export type RecordScoreRequest = z.infer<typeof RecordScoreRequestSchema>;

export const ConfigurePhasesRequestSchema = z.object({
  phaseNames: z.array(z.string()),
});

export type ConfigurePhasesRequest = z.infer<typeof ConfigurePhasesRequestSchema>;

export const TriggerSnapshotRequestSchema = z.object({
  triggerType: SnapshotTriggerSchema,
  description: z.string().optional(),
  createdByPlayerId: z.string().uuid().optional(),
});

export type TriggerSnapshotRequest = z.infer<typeof TriggerSnapshotRequestSchema>;

export const UpdateNotesRequestSchema = z.object({
  notes: z.string().nullable(),
});

export type UpdateNotesRequest = z.infer<typeof UpdateNotesRequestSchema>;
