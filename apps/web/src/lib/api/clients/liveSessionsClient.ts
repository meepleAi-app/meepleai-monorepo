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
  ScoreParseResultSchema,
  type ScoreParseResult,
  type ParseScoreRequest,
  type ConfirmScoreRequest,
} from '../schemas/score-tracking.schemas';

import type { HttpClient } from '../core/httpClient';

const BASE = '/api/v1/live-sessions';

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
  };
}
