/**
 * Sessions Client (FE-IMP-005)
 *
 * Modular client for GameManagement bounded context (Sessions).
 * Covers: Game sessions management (active, history, CRUD, lifecycle)
 */

import {
  GameSessionDtoSchema,
  PaginatedSessionsResponseSchema,
  SessionQuotaResponseSchema,
  type GameSessionDto,
  type PaginatedSessionsResponse,
  type SessionQuotaResponse,
} from '../schemas';
import {
  ToolkitSessionStateDtoSchema,
  type ToolkitSessionStateDto,
  type UpdateWidgetStateRequest,
} from '../schemas/toolkit.schemas';

import type { HttpClient } from '../core/httpClient';

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

    // ========== Game State Management (Issue #2406) ==========

    /**
     * Initialize game state for a session
     * @param sessionId Session ID
     * @param templateId GameStateTemplate ID
     */
    async initializeState(sessionId: string, templateId: string): Promise<unknown> {
      return httpClient.post(`/api/v1/sessions/${encodeURIComponent(sessionId)}/state/initialize`, {
        templateId,
      });
    },

    /**
     * Get current game state
     * @param sessionId Session ID
     */
    async getState(sessionId: string): Promise<unknown> {
      return httpClient.get(`/api/v1/sessions/${encodeURIComponent(sessionId)}/state`);
    },

    /**
     * Update game state
     * @param sessionId Session ID
     * @param stateJson Updated state as JSON
     */
    async updateState(sessionId: string, stateJson: string): Promise<unknown> {
      return httpClient.patch(`/api/v1/sessions/${encodeURIComponent(sessionId)}/state`, {
        stateJson,
      });
    },

    /**
     * Create state snapshot
     * @param sessionId Session ID
     * @param description Snapshot description
     * @param turnNumber Optional turn number
     */
    async createSnapshot(
      sessionId: string,
      description: string,
      turnNumber?: number
    ): Promise<unknown> {
      return httpClient.post(`/api/v1/sessions/${encodeURIComponent(sessionId)}/state/snapshots`, {
        description,
        turnNumber,
      });
    },

    /**
     * Get state snapshots
     * @param sessionId Session ID
     */
    async getSnapshots(sessionId: string): Promise<unknown[]> {
      const response = await httpClient.get<unknown[]>(
        `/api/v1/sessions/${encodeURIComponent(sessionId)}/state/snapshots`
      );
      return response || [];
    },

    /**
     * Restore state from snapshot
     * @param sessionId Session ID
     * @param snapshotId Snapshot ID
     */
    async restoreSnapshot(sessionId: string, snapshotId: string): Promise<unknown> {
      return httpClient.post(
        `/api/v1/sessions/${encodeURIComponent(sessionId)}/state/restore/${encodeURIComponent(snapshotId)}`
      );
    },

    // ========== Session Quota (Issue #3075) ==========

    /**
     * Get current user's session quota
     * GET /api/v1/users/{userId}/session-quota
     *
     * Returns session quota information including:
     * - Current active sessions count
     * - Maximum allowed sessions for user's tier
     * - Remaining slots available
     * - Whether user can create new sessions
     *
     * @param userId User ID (GUID format)
     * @returns Session quota information
     */
    async getQuota(userId: string): Promise<SessionQuotaResponse> {
      const response = await httpClient.get(
        `/api/v1/users/${encodeURIComponent(userId)}/session-quota`,
        SessionQuotaResponseSchema
      );

      if (!response) {
        // Return default response for unauthenticated/error cases
        return {
          currentSessions: 0,
          maxSessions: 1,
          remainingSlots: 1,
          canCreateNew: true,
          isUnlimited: false,
          userTier: 'free',
        };
      }

      return response;
    },

    // ========== Toolkit Session State (Issue #5148 — Epic B5) ==========

    /**
     * Get the current widget runtime states for a session's toolkit.
     * Returns null if no state has been saved yet (204 from API).
     * GET /api/v1/game-sessions/{sessionId}/toolkit-state
     */
    async getToolkitSessionState(sessionId: string): Promise<ToolkitSessionStateDto | null> {
      return httpClient.get<ToolkitSessionStateDto>(
        `/api/v1/game-sessions/${sessionId}/toolkit-state`,
        ToolkitSessionStateDtoSchema
      );
    },

    /**
     * Update the runtime state for a single widget in a session.
     * PATCH /api/v1/game-sessions/{sessionId}/toolkit-state/{widgetType}?toolkitId={toolkitId}
     */
    async updateToolkitWidgetState(
      sessionId: string,
      widgetType: string,
      toolkitId: string,
      request: UpdateWidgetStateRequest
    ): Promise<ToolkitSessionStateDto> {
      const data = await httpClient.patch<ToolkitSessionStateDto>(
        `/api/v1/game-sessions/${sessionId}/toolkit-state/${widgetType}?toolkitId=${toolkitId}`,
        request,
        ToolkitSessionStateDtoSchema
      );
      if (!data) throw new Error('Failed to update widget state');
      return data;
    },
  };
}

export type SessionsClient = ReturnType<typeof createSessionsClient>;
