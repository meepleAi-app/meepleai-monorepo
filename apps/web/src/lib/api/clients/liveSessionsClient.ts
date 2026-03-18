/**
 * Live Sessions API Client
 *
 * Issue #5041 — Sessions Redesign Phase 1
 *
 * Maps to LiveSessionEndpoints.cs (~24 endpoints).
 * Handles session lifecycle, players, scoring, teams, turns, phases, snapshots.
 */

import { z } from 'zod';

import {
  RuleDisputeResponseSchema,
  CreatePauseSnapshotResponseSchema,
  type RuleDisputeResponse,
  type CreatePauseSnapshotResponse,
} from '../schemas/improvvisata.schemas';
import {
  LiveSessionDtoSchema,
  LiveSessionSummaryDtoSchema,
  LiveSessionPlayerDtoSchema,
  LiveSessionRoundScoreDtoSchema,
  SessionSnapshotDtoSchema,
  SessionToolsDtoSchema,
  TurnPhasesDtoSchema,
  type LiveSessionDto,
  type LiveSessionSummaryDto,
  type LiveSessionPlayerDto,
  type LiveSessionRoundScoreDto,
  type SessionSnapshotDto,
  type SessionToolsDto,
  type TurnPhasesDto,
  type CreateLiveSessionRequest,
  type AddPlayerRequest,
  type UpdateTurnOrderRequest,
  type CreateTeamRequest,
  type RecordScoreRequest,
  type ConfigurePhasesRequest,
  type TriggerSnapshotRequest,
  type UpdateNotesRequest,
} from '../schemas/live-sessions.schemas';
import {
  SessionSaveResultSchema,
  SessionResumeContextSchema,
  type SessionSaveResult,
  type SessionResumeContext,
} from '../schemas/save-resume.schemas';
import {
  ScoreParseResultSchema,
  type ScoreParseResult,
  type ParseScoreRequest,
  type ConfirmScoreRequest,
} from '../schemas/score-tracking.schemas';

import type { HttpClient } from '../core/httpClient';

// ─── Setup Wizard Types ──────────────────────────────────────────────────────

export interface SetupChecklistComponent {
  name: string;
  quantity: number;
  checked: boolean;
}

export interface SetupChecklistStep {
  order: number;
  instruction: string;
  completed: boolean;
}

export interface SetupChecklistData {
  components: SetupChecklistComponent[];
  steps: SetupChecklistStep[];
}

const BASE = '/api/v1/live-sessions';
const GAME_NIGHT_BASE = '/api/v1/game-night';

export interface LiveSessionsClient {
  // ========== Queries ==========

  /** Get all active sessions for the current user */
  getActive(): Promise<LiveSessionSummaryDto[]>;

  /** Get a session by ID */
  getSession(sessionId: string): Promise<LiveSessionDto>;

  /** Get a session by join code */
  getByCode(code: string): Promise<LiveSessionDto>;

  /** Get scores for a session */
  getScores(sessionId: string): Promise<LiveSessionRoundScoreDto[]>;

  /** Get players for a session */
  getPlayers(sessionId: string): Promise<LiveSessionPlayerDto[]>;

  /** Get turn phases configuration */
  getPhases(sessionId: string): Promise<TurnPhasesDto>;

  /** Get session tools (toolkit) */
  getTools(sessionId: string): Promise<SessionToolsDto>;

  // ========== Session Lifecycle ==========

  /** Create a new live session */
  createSession(request: CreateLiveSessionRequest): Promise<string>;

  /** Start a session (transition to InProgress) */
  startSession(sessionId: string): Promise<void>;

  /** Pause an in-progress session */
  pauseSession(sessionId: string): Promise<void>;

  /** Resume a paused session */
  resumeSession(sessionId: string): Promise<void>;

  /** Complete/finalize a session */
  completeSession(sessionId: string): Promise<void>;

  /** Save session state */
  saveSession(sessionId: string): Promise<void>;

  // ========== Players ==========

  /** Add a player to the session */
  addPlayer(sessionId: string, request: AddPlayerRequest): Promise<string>;

  /** Remove a player from the session */
  removePlayer(sessionId: string, playerId: string): Promise<void>;

  /** Update player turn order */
  updateTurnOrder(sessionId: string, request: UpdateTurnOrderRequest): Promise<void>;

  // ========== Teams ==========

  /** Create a team */
  createTeam(sessionId: string, request: CreateTeamRequest): Promise<string>;

  /** Assign a player to a team */
  assignPlayerToTeam(sessionId: string, teamId: string, playerId: string): Promise<void>;

  // ========== Scoring ==========

  /** Record a new score entry */
  recordScore(sessionId: string, request: RecordScoreRequest): Promise<void>;

  /** Edit an existing score entry */
  editScore(sessionId: string, request: RecordScoreRequest): Promise<void>;

  // ========== Turns & Phases ==========

  /** Advance to the next turn */
  advanceTurn(sessionId: string): Promise<void>;

  /** Advance to the next phase */
  advancePhase(sessionId: string): Promise<void>;

  /** Configure phase names */
  configurePhases(sessionId: string, request: ConfigurePhasesRequest): Promise<void>;

  // ========== Snapshots ==========

  /** Trigger a snapshot */
  triggerSnapshot(
    sessionId: string,
    request: TriggerSnapshotRequest
  ): Promise<SessionSnapshotDto | null>;

  // ========== Notes ==========

  /** Update session notes */
  updateNotes(sessionId: string, request: UpdateNotesRequest): Promise<void>;

  // ========== AI Score Tracking (Issue #121) ==========

  /** Parse a natural language message for score data, optionally auto-record */
  parseScore(sessionId: string, request: ParseScoreRequest): Promise<ScoreParseResult>;

  /** Confirm and record a previously parsed score */
  confirmScore(sessionId: string, request: ConfirmScoreRequest): Promise<void>;

  // ========== Enhanced Save/Resume (Issue #122) ==========

  /** Save complete session state: pause + snapshot + agent persist + recap */
  saveComplete(sessionId: string): Promise<SessionSaveResult>;

  /** Get session resume context with recap, scores, and photos */
  getResumeContext(sessionId: string): Promise<SessionResumeContext>;

  // ========== Game Night Improvvisata ==========

  /**
   * Submit a rule dispute and get an AI verdict.
   * POST /api/v1/game-night/sessions/{sessionId}/disputes
   */
  submitDispute(
    sessionId: string,
    description: string,
    raisedByPlayerName: string
  ): Promise<RuleDisputeResponse>;

  /**
   * Create a pause snapshot for the session.
   * POST /api/v1/game-night/sessions/{sessionId}/pause-snapshot
   */
  createPauseSnapshot(sessionId: string): Promise<CreatePauseSnapshotResponse>;

  // ========== Dispute v2 ==========

  /**
   * Open a structured rule dispute.
   * POST /api/v1/live-sessions/{sessionId}/disputes
   */
  openStructuredDispute(
    sessionId: string,
    initiatorPlayerId: string,
    initiatorClaim: string,
    respondentPlayerId?: string
  ): Promise<string>;

  /**
   * Respond to an existing dispute with a counter-claim.
   * PUT /api/v1/live-sessions/{sessionId}/disputes/{disputeId}/respond
   */
  respondToDispute(
    sessionId: string,
    disputeId: string,
    respondentPlayerId: string,
    respondentClaim: string
  ): Promise<void>;

  /**
   * Trigger the respondent timeout for a dispute.
   * POST /api/v1/live-sessions/{sessionId}/disputes/{disputeId}/timeout
   */
  respondentTimeout(sessionId: string, disputeId: string): Promise<void>;

  /**
   * Cast a vote on a dispute verdict.
   * POST /api/v1/live-sessions/{sessionId}/disputes/{disputeId}/vote
   */
  castVote(
    sessionId: string,
    disputeId: string,
    playerId: string,
    acceptsVerdict: boolean
  ): Promise<void>;

  /**
   * Tally votes on a dispute and determine final outcome.
   * POST /api/v1/live-sessions/{sessionId}/disputes/{disputeId}/tally
   */
  tallyVotes(sessionId: string, disputeId: string, overrideRule?: string): Promise<void>;

  // ========== Setup Wizard (Task 8) ==========

  /**
   * Generate a setup checklist for the session.
   * POST /api/v1/live-sessions/{sessionId}/setup-checklist
   */
  generateSetupChecklist(sessionId: string, playerCount: number): Promise<SetupChecklistData>;

  /**
   * Update the setup checklist for the session.
   * PUT /api/v1/live-sessions/{sessionId}/setup-checklist
   */
  updateSetupChecklist(sessionId: string, data: SetupChecklistData): Promise<void>;
}

export function createLiveSessionsClient({
  httpClient,
}: {
  httpClient: HttpClient;
}): LiveSessionsClient {
  return {
    // ========== Queries ==========

    async getActive() {
      const response = await httpClient.get<LiveSessionSummaryDto[]>(`${BASE}/active`);
      return z.array(LiveSessionSummaryDtoSchema).parse(response ?? []);
    },

    async getSession(sessionId) {
      const response = await httpClient.get<LiveSessionDto>(
        `${BASE}/${encodeURIComponent(sessionId)}`
      );
      if (!response) throw new Error('Session not found');
      return LiveSessionDtoSchema.parse(response);
    },

    async getByCode(code) {
      const response = await httpClient.get<LiveSessionDto>(
        `${BASE}/code/${encodeURIComponent(code)}`
      );
      if (!response) throw new Error('Session not found');
      return LiveSessionDtoSchema.parse(response);
    },

    async getScores(sessionId) {
      const response = await httpClient.get<LiveSessionRoundScoreDto[]>(
        `${BASE}/${encodeURIComponent(sessionId)}/scores`
      );
      return z.array(LiveSessionRoundScoreDtoSchema).parse(response ?? []);
    },

    async getPlayers(sessionId) {
      const response = await httpClient.get<LiveSessionPlayerDto[]>(
        `${BASE}/${encodeURIComponent(sessionId)}/players`
      );
      return z.array(LiveSessionPlayerDtoSchema).parse(response ?? []);
    },

    async getPhases(sessionId) {
      const response = await httpClient.get<TurnPhasesDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/phases`
      );
      if (!response) throw new Error('Phases not found');
      return TurnPhasesDtoSchema.parse(response);
    },

    async getTools(sessionId) {
      const response = await httpClient.get<SessionToolsDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/tools`
      );
      if (!response) throw new Error('Tools not found');
      return SessionToolsDtoSchema.parse(response);
    },

    // ========== Session Lifecycle ==========

    async createSession(request) {
      const response = await httpClient.post<string>(`${BASE}`, request);
      return response as string;
    },

    async startSession(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/start`);
    },

    async pauseSession(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/pause`);
    },

    async resumeSession(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/resume`);
    },

    async completeSession(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/complete`);
    },

    async saveSession(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/save`);
    },

    // ========== Players ==========

    async addPlayer(sessionId, request) {
      const response = await httpClient.post<string>(
        `${BASE}/${encodeURIComponent(sessionId)}/players`,
        request
      );
      return response as string;
    },

    async removePlayer(sessionId, playerId) {
      await httpClient.delete(
        `${BASE}/${encodeURIComponent(sessionId)}/players/${encodeURIComponent(playerId)}`
      );
    },

    async updateTurnOrder(sessionId, request) {
      await httpClient.put(`${BASE}/${encodeURIComponent(sessionId)}/turn-order`, request);
    },

    // ========== Teams ==========

    async createTeam(sessionId, request) {
      const response = await httpClient.post<string>(
        `${BASE}/${encodeURIComponent(sessionId)}/teams`,
        request
      );
      return response as string;
    },

    async assignPlayerToTeam(sessionId, teamId, playerId) {
      await httpClient.put(
        `${BASE}/${encodeURIComponent(sessionId)}/teams/${encodeURIComponent(teamId)}/players/${encodeURIComponent(playerId)}`,
        {}
      );
    },

    // ========== Scoring ==========

    async recordScore(sessionId, request) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/scores`, request);
    },

    async editScore(sessionId, request) {
      await httpClient.put(`${BASE}/${encodeURIComponent(sessionId)}/scores`, request);
    },

    // ========== Turns & Phases ==========

    async advanceTurn(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/advance-turn`);
    },

    async advancePhase(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/advance-phase`);
    },

    async configurePhases(sessionId, request) {
      await httpClient.put(`${BASE}/${encodeURIComponent(sessionId)}/phases`, request);
    },

    // ========== Snapshots ==========

    async triggerSnapshot(sessionId, request) {
      const response = await httpClient.post<SessionSnapshotDto | null>(
        `${BASE}/${encodeURIComponent(sessionId)}/trigger-snapshot`,
        request
      );
      if (!response) return null;
      return SessionSnapshotDtoSchema.parse(response);
    },

    // ========== Notes ==========

    async updateNotes(sessionId, request) {
      await httpClient.put(`${BASE}/${encodeURIComponent(sessionId)}/notes`, request);
    },

    // ========== AI Score Tracking (Issue #121) ==========

    async parseScore(sessionId, request) {
      const response = await httpClient.post<ScoreParseResult>(
        `${BASE}/${encodeURIComponent(sessionId)}/scores/parse`,
        request
      );
      if (!response) throw new Error('Parse score failed');
      return ScoreParseResultSchema.parse(response);
    },

    async confirmScore(sessionId, request) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/scores/confirm`, request);
    },

    // ========== Enhanced Save/Resume (Issue #122) ==========

    async saveComplete(sessionId) {
      const response = await httpClient.post<SessionSaveResult>(
        `${BASE}/${encodeURIComponent(sessionId)}/save-complete`
      );
      if (!response) throw new Error('Save complete failed');
      return SessionSaveResultSchema.parse(response);
    },

    async getResumeContext(sessionId) {
      const response = await httpClient.get<SessionResumeContext>(
        `${BASE}/${encodeURIComponent(sessionId)}/resume-context`
      );
      if (!response) throw new Error('Resume context not found');
      return SessionResumeContextSchema.parse(response);
    },

    // ========== Game Night Improvvisata ==========

    async submitDispute(sessionId, description, raisedByPlayerName) {
      const response = await httpClient.post<RuleDisputeResponse>(
        `${GAME_NIGHT_BASE}/sessions/${encodeURIComponent(sessionId)}/disputes`,
        { description, raisedByPlayerName }
      );
      if (!response) throw new Error('Dispute submission failed');
      return RuleDisputeResponseSchema.parse(response);
    },

    async createPauseSnapshot(sessionId) {
      const response = await httpClient.post<CreatePauseSnapshotResponse>(
        `${GAME_NIGHT_BASE}/sessions/${encodeURIComponent(sessionId)}/save`,
        {}
      );
      if (!response) throw new Error('Pause snapshot creation failed');
      return CreatePauseSnapshotResponseSchema.parse(response);
    },

    // ========== Dispute v2 ==========

    async openStructuredDispute(sessionId, initiatorPlayerId, initiatorClaim, respondentPlayerId) {
      const response = await httpClient.post<string>(
        `${BASE}/${encodeURIComponent(sessionId)}/disputes`,
        {
          initiatorPlayerId,
          initiatorClaim,
          respondentPlayerId: respondentPlayerId ?? null,
        }
      );
      return response as string;
    },

    async respondToDispute(sessionId, disputeId, respondentPlayerId, respondentClaim) {
      await httpClient.put(
        `${BASE}/${encodeURIComponent(sessionId)}/disputes/${encodeURIComponent(disputeId)}/respond`,
        { respondentPlayerId, respondentClaim }
      );
    },

    async respondentTimeout(sessionId, disputeId) {
      await httpClient.post(
        `${BASE}/${encodeURIComponent(sessionId)}/disputes/${encodeURIComponent(disputeId)}/timeout`
      );
    },

    async castVote(sessionId, disputeId, playerId, acceptsVerdict) {
      await httpClient.post(
        `${BASE}/${encodeURIComponent(sessionId)}/disputes/${encodeURIComponent(disputeId)}/vote`,
        { playerId, acceptsVerdict }
      );
    },

    async tallyVotes(sessionId, disputeId, overrideRule) {
      await httpClient.post(
        `${BASE}/${encodeURIComponent(sessionId)}/disputes/${encodeURIComponent(disputeId)}/tally`,
        { overrideRule: overrideRule ?? null }
      );
    },

    // ========== Setup Wizard (Task 8) ==========

    async generateSetupChecklist(sessionId, playerCount) {
      const response = await httpClient.post<SetupChecklistData>(
        `${BASE}/${encodeURIComponent(sessionId)}/setup-checklist`,
        { playerCount }
      );
      if (!response) throw new Error('Setup checklist generation failed');
      return response;
    },

    async updateSetupChecklist(sessionId, data) {
      await httpClient.put(`${BASE}/${encodeURIComponent(sessionId)}/setup-checklist`, data);
    },
  };
}
