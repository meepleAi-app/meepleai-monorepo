/**
 * Session Flow v2.1 API Types
 *
 * TypeScript types mirroring the backend DTOs for the Session Flow v2.1 endpoints.
 * Maps to: SessionFlowEndpoints.cs, SessionPlayerActionsEndpoints.cs,
 * GameEndpoints.cs (CreateSession), CompleteGameNightCommand.
 */

// ─── Create Session ─────────────────────────────────────────────────────────

export interface CreateSessionRequest {
  sessionType: string;
  sessionDate?: string;
  location?: string;
  participants: CreateSessionParticipant[];
  gameNightEventId?: string;
  guestNames?: string[];
}

export interface CreateSessionParticipant {
  displayName: string;
  isOwner: boolean;
}

export interface CreateSessionResult {
  sessionId: string;
  sessionCode: string;
  participants: SessionParticipantDto[];
  gameNightEventId: string;
  gameNightWasCreated: boolean;
  agentDefinitionId: string | null;
  toolkitId: string | null;
}

export interface SessionParticipantDto {
  id: string;
  userId: string | null;
  displayName: string;
  isOwner: boolean;
  joinOrder: number;
  finalRank: number | null;
  totalScore: number;
}

// ─── Turn Order ─────────────────────────────────────────────────────────────

export interface SetTurnOrderRequest {
  method: 'manual' | 'random';
  order?: string[];
}

export interface SetTurnOrderResult {
  method: string;
  seed: number | null;
  order: string[];
}

// ─── Advance Turn ───────────────────────────────────────────────────────────

export interface AdvanceTurnResult {
  fromIndex: number;
  toIndex: number;
  fromParticipantId: string;
  toParticipantId: string;
}

// ─── Roll Dice ──────────────────────────────────────────────────────────────

export interface RollSessionDiceRequest {
  participantId: string;
  formula: string;
  label?: string;
}

export interface RollSessionDiceResult {
  diceRollId: string;
  formula: string;
  rolls: number[];
  modifier: number;
  total: number;
  timestamp: string;
}

// ─── Upsert Score ───────────────────────────────────────────────────────────

export interface UpsertScoreRequest {
  participantId: string;
  newValue: number;
  roundNumber?: number;
  category?: string;
  reason?: string;
}

export interface UpsertScoreResult {
  scoreEntryId: string;
  oldValue: number;
  newValue: number;
}

// ─── Diary ──────────────────────────────────────────────────────────────────

/**
 * Diary entry from the append-only session event log.
 *
 * Named `DiaryEntryDto` (not `SessionEventDto`) to avoid collision with the
 * identically-named type in `toolkit.ts` which serves the legacy session-event
 * endpoints. The backend record is `SessionEventDto` in the `.DTOs` namespace.
 */
export interface DiaryEntryDto {
  id: string;
  sessionId: string;
  gameNightId: string | null;
  eventType: string;
  timestamp: string;
  payload: string | null;
  createdBy: string | null;
  source: string | null;
}

export interface DiaryQueryParams {
  eventTypes?: string;
  since?: string;
  limit?: number;
}

// ─── Current Session ────────────────────────────────────────────────────────

export interface CurrentSessionDto {
  sessionId: string;
  gameId: string;
  status: string;
  sessionCode: string;
  sessionDate: string;
  updatedAt: string | null;
  gameNightEventId: string | null;
}

// ─── KB Readiness ───────────────────────────────────────────────────────────

export interface KbReadinessDto {
  isReady: boolean;
  state: string;
  readyPdfCount: number;
  failedPdfCount: number;
  warnings: string[];
}

// ─── Complete Game Night ────────────────────────────────────────────────────

export interface CompleteGameNightResult {
  gameNightEventId: string;
  sessionCount: number;
  finalizedSessionCount: number;
  durationSeconds: number;
}
