/**
 * Sessions Client (FE-IMP-005)
 *
 * Modular client for GameManagement bounded context (Sessions).
 * Covers: Game sessions management (active, history, CRUD, lifecycle)
 */

import type { HttpClient } from '../core/httpClient';
import {
  GameSessionDtoSchema,
  PaginatedSessionsResponseSchema,
  type GameSessionDto,
  type PaginatedSessionsResponse,
} from '../schemas';

export interface CreateSessionsClientParams {
  httpClient: HttpClient;
}

export interface SessionHistoryFilters {
  gameId?: string;
  startDate?: string;
  endDate?: string;
  limit?: number;
  offset?: number;
}

export interface StartSessionRequest {
  gameId: string;
  players: Array<{
    playerName: string;
    playerOrder: number;
    color?: string | null;
  }>;
  notes?: string | null;
}

export interface CompleteSessionRequest {
  winnerName?: string | null;
}

/**
 * Sessions API client with Zod validation
 */
export function createSessionsClient({ httpClient }: CreateSessionsClientParams) {
  return {
    // ========== Active Sessions ==========

    /**
     * Get all active sessions with optional pagination
     * @param limit Maximum number of sessions to return (default: 20)
     * @param offset Number of sessions to skip (default: 0)
     */
    async getActive(limit: number = 20, offset: number = 0): Promise<PaginatedSessionsResponse> {
      const params = new URLSearchParams();
      params.append('limit', limit.toString());
      params.append('offset', offset.toString());

      const response = await httpClient.get(
        `/api/v1/sessions/active?${params}`,
        PaginatedSessionsResponseSchema
      );

      if (!response) {
        return {
          sessions: [],
          total: 0,
          page: Math.floor(offset / limit) + 1,
          pageSize: limit,
        };
      }

      return response;
    },

    // ========== Session History ==========

    /**
     * Get session history with optional filters
     * @param filters Optional filters for game, date range, and pagination
     */
    async getHistory(filters?: SessionHistoryFilters): Promise<PaginatedSessionsResponse> {
      const params = new URLSearchParams();
      if (filters?.gameId) params.append('gameId', filters.gameId);
      if (filters?.startDate) params.append('startDate', filters.startDate);
      if (filters?.endDate) params.append('endDate', filters.endDate);
      if (filters?.limit) params.append('limit', filters.limit.toString());
      if (filters?.offset) params.append('offset', filters.offset.toString());

      const response = await httpClient.get(
        `/api/v1/sessions/history?${params}`,
        PaginatedSessionsResponseSchema
      );

      if (!response) {
        const limit = filters?.limit || 20;
        const offset = filters?.offset || 0;
        return {
          sessions: [],
          total: 0,
          page: Math.floor(offset / limit) + 1,
          pageSize: limit,
        };
      }

      return response;
    },

    // ========== Session CRUD ==========

    /**
     * Get a single session by ID
     * @param id Session ID (GUID format)
     */
    async getById(id: string): Promise<GameSessionDto | null> {
      return httpClient.get(`/api/v1/sessions/${encodeURIComponent(id)}`, GameSessionDtoSchema);
    },

    /**
     * Start a new game session
     * @param request Session start request with game ID and players
     */
    async start(request: StartSessionRequest): Promise<GameSessionDto> {
      return httpClient.post('/api/v1/sessions', request, GameSessionDtoSchema);
    },

    // ========== Session Lifecycle ==========

    /**
     * Pause an active session
     * @param id Session ID (GUID format)
     */
    async pause(id: string): Promise<GameSessionDto> {
      return httpClient.post(
        `/api/v1/sessions/${encodeURIComponent(id)}/pause`,
        {},
        GameSessionDtoSchema
      );
    },

    /**
     * Resume a paused session
     * @param id Session ID (GUID format)
     */
    async resume(id: string): Promise<GameSessionDto> {
      return httpClient.post(
        `/api/v1/sessions/${encodeURIComponent(id)}/resume`,
        {},
        GameSessionDtoSchema
      );
    },

    /**
     * End a session without marking it complete
     * @param id Session ID (GUID format)
     * @param winnerName Optional winner name
     */
    async end(id: string, winnerName?: string | null): Promise<GameSessionDto> {
      return httpClient.post(
        `/api/v1/sessions/${encodeURIComponent(id)}/end`,
        { winnerName },
        GameSessionDtoSchema
      );
    },

    /**
     * Complete a session with winner information
     * @param id Session ID (GUID format)
     * @param request Completion request with optional winner name
     */
    async complete(id: string, request?: CompleteSessionRequest): Promise<GameSessionDto> {
      return httpClient.post(
        `/api/v1/sessions/${encodeURIComponent(id)}/complete`,
        request || {},
        GameSessionDtoSchema
      );
    },

    /**
     * Abandon a session
     * @param id Session ID (GUID format)
     */
    async abandon(id: string): Promise<GameSessionDto> {
      return httpClient.post(
        `/api/v1/sessions/${encodeURIComponent(id)}/abandon`,
        {},
        GameSessionDtoSchema
      );
    },
  };
}

export type SessionsClient = ReturnType<typeof createSessionsClient>;
