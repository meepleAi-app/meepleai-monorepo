/**
 * Session Flow v2.1 API Client
 *
 * Provides typed functions for all 12 Session Flow v2.1 endpoints.
 * Uses the project's standard HttpClient pattern (credentials: 'include',
 * JSON body, centralized error handling).
 *
 * Endpoints covered:
 *   POST   /api/v1/games/{gameId}/sessions            — createSession
 *   POST   /api/v1/sessions/{id}/pause                — pauseSession
 *   POST   /api/v1/sessions/{id}/resume               — resumeSession
 *   PUT    /api/v1/sessions/{id}/turn-order            — setTurnOrder
 *   POST   /api/v1/sessions/{id}/turn/advance          — advanceTurn
 *   POST   /api/v1/game-sessions/{id}/actions/roll-dice — rollSessionDice
 *   POST   /api/v1/sessions/{id}/scores-with-diary     — upsertScore
 *   GET    /api/v1/sessions/{id}/diary                 — getSessionDiary
 *   GET    /api/v1/sessions/current                    — getCurrentSession
 *   GET    /api/v1/games/{id}/kb-readiness             — getKbReadiness
 *   GET    /api/v1/game-nights/{id}/diary              — getGameNightDiary
 *   POST   /api/v1/game-nights/{id}/complete           — completeGameNight
 */

import type {
  CreateSessionRequest,
  CreateSessionResult,
  SetTurnOrderRequest,
  SetTurnOrderResult,
  AdvanceTurnResult,
  RollSessionDiceRequest,
  RollSessionDiceResult,
  UpsertScoreRequest,
  UpsertScoreResult,
  DiaryEntryDto,
  DiaryQueryParams,
  CurrentSessionDto,
  KbReadinessDto,
  CompleteGameNightResult,
} from './types';
import type { HttpClient } from '../core/httpClient';

const SESSION_BASE = '/api/v1/sessions';
const GAME_SESSION_BASE = '/api/v1/game-sessions';
const GAME_NIGHT_BASE = '/api/v1/game-nights';
const GAMES_BASE = '/api/v1/games';

// ─── Client Interface ───────────────────────────────────────────────────────

export interface SessionFlowClient {
  /** Create a new session for a game (auto-creates ad-hoc GameNight when gameNightEventId is null). */
  createSession(gameId: string, request: CreateSessionRequest): Promise<CreateSessionResult>;

  /** Pause an active session. */
  pauseSession(sessionId: string): Promise<void>;

  /** Resume a paused session (auto-pauses other active sessions in the same night). */
  resumeSession(sessionId: string): Promise<void>;

  /** Set the turn order for a session (manual list or cryptographic shuffle). */
  setTurnOrder(sessionId: string, request: SetTurnOrderRequest): Promise<SetTurnOrderResult>;

  /** Advance the turn index to the next participant (cyclic). */
  advanceTurn(sessionId: string): Promise<AdvanceTurnResult>;

  /** Roll dice using a formula (e.g. "2d6+3"). Uses the existing session tracking endpoint. */
  rollSessionDice(
    sessionId: string,
    request: RollSessionDiceRequest
  ): Promise<RollSessionDiceResult>;

  /** Upsert a participant score and emit a score_updated diary event. */
  upsertScore(sessionId: string, request: UpsertScoreRequest): Promise<UpsertScoreResult>;

  /** Read the append-only diary for a single session (chronological). */
  getSessionDiary(sessionId: string, params?: DiaryQueryParams): Promise<DiaryEntryDto[]>;

  /** Return the caller's latest Active or Paused session (orphan recovery). Returns null on 204. */
  getCurrentSession(): Promise<CurrentSessionDto | null>;

  /** Probe whether the Knowledge Base for a game is ready to power an agent. */
  getKbReadiness(gameId: string): Promise<KbReadinessDto>;

  /** Read the append-only diary for a whole game night (unions all attached sessions). */
  getGameNightDiary(gameNightId: string, params?: DiaryQueryParams): Promise<DiaryEntryDto[]>;

  /** Complete a game night: cascade-finalize all sessions and emit diary events. */
  completeGameNight(gameNightId: string): Promise<CompleteGameNightResult>;
}

// ─── Factory ────────────────────────────────────────────────────────────────

export function createSessionFlowClient({
  httpClient,
}: {
  httpClient: HttpClient;
}): SessionFlowClient {
  return {
    async createSession(gameId, request) {
      const response = await httpClient.post<CreateSessionResult>(
        `${GAMES_BASE}/${encodeURIComponent(gameId)}/sessions`,
        request
      );
      if (!response) throw new Error('Create session returned empty response');
      return response;
    },

    async pauseSession(sessionId) {
      await httpClient.post(`${SESSION_BASE}/${encodeURIComponent(sessionId)}/pause`);
    },

    async resumeSession(sessionId) {
      await httpClient.post(`${SESSION_BASE}/${encodeURIComponent(sessionId)}/resume`);
    },

    async setTurnOrder(sessionId, request) {
      const response = await httpClient.put<SetTurnOrderResult>(
        `${SESSION_BASE}/${encodeURIComponent(sessionId)}/turn-order`,
        request
      );
      if (!response) throw new Error('Set turn order returned empty response');
      return response;
    },

    async advanceTurn(sessionId) {
      const response = await httpClient.post<AdvanceTurnResult>(
        `${SESSION_BASE}/${encodeURIComponent(sessionId)}/turn/advance`
      );
      if (!response) throw new Error('Advance turn returned empty response');
      return response;
    },

    async rollSessionDice(sessionId, request) {
      const response = await httpClient.post<RollSessionDiceResult>(
        `${GAME_SESSION_BASE}/${encodeURIComponent(sessionId)}/actions/roll-dice`,
        request
      );
      if (!response) throw new Error('Roll dice returned empty response');
      return response;
    },

    async upsertScore(sessionId, request) {
      const response = await httpClient.post<UpsertScoreResult>(
        `${SESSION_BASE}/${encodeURIComponent(sessionId)}/scores-with-diary`,
        request
      );
      if (!response) throw new Error('Upsert score returned empty response');
      return response;
    },

    async getSessionDiary(sessionId, params) {
      const qs = buildDiaryQueryString(params);
      const response = await httpClient.get<DiaryEntryDto[]>(
        `${SESSION_BASE}/${encodeURIComponent(sessionId)}/diary${qs}`
      );
      return response ?? [];
    },

    async getCurrentSession() {
      // GET /sessions/current returns 200 with body or 204 No Content.
      // HttpClient returns null for 204 (via `undefined as T`).
      const response = await httpClient.get<CurrentSessionDto>(`${SESSION_BASE}/current`);
      return response ?? null;
    },

    async getKbReadiness(gameId) {
      const response = await httpClient.get<KbReadinessDto>(
        `${GAMES_BASE}/${encodeURIComponent(gameId)}/kb-readiness`
      );
      if (!response) throw new Error('KB readiness returned empty response');
      return response;
    },

    async getGameNightDiary(gameNightId, params) {
      const qs = buildDiaryQueryString(params);
      const response = await httpClient.get<DiaryEntryDto[]>(
        `${GAME_NIGHT_BASE}/${encodeURIComponent(gameNightId)}/diary${qs}`
      );
      return response ?? [];
    },

    async completeGameNight(gameNightId) {
      const response = await httpClient.post<CompleteGameNightResult>(
        `${GAME_NIGHT_BASE}/${encodeURIComponent(gameNightId)}/complete`
      );
      if (!response) throw new Error('Complete game night returned empty response');
      return response;
    },
  };
}

// ─── Helpers ────────────────────────────────────────────────────────────────

function buildDiaryQueryString(params?: DiaryQueryParams): string {
  if (!params) return '';
  const qs = new URLSearchParams();
  if (params.eventTypes) qs.append('eventTypes', params.eventTypes);
  if (params.since) qs.append('since', params.since);
  if (params.limit !== undefined) qs.append('limit', params.limit.toString());
  const str = qs.toString();
  return str ? `?${str}` : '';
}
