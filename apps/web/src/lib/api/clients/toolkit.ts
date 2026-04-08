/**
 * Toolkit API Client
 *
 * Endpoints for the Default Game Toolkit (session events and dice presets).
 *
 * Endpoints:
 * - POST   /api/v1/session-events                       — Add session event
 * - GET    /api/v1/session-events/by-session/{sessionId} — List session events
 * - GET    /api/v1/game-toolkits/{toolkitId}/dice-presets — Get user dice presets
 * - POST   /api/v1/game-toolkits/{toolkitId}/dice-presets — Add dice preset
 * - DELETE  /api/v1/game-toolkits/{toolkitId}/dice-presets/{name} — Remove dice preset
 */

import type { HttpClient } from '../core/httpClient';

// ============================================================================
// Types
// ============================================================================

export interface AddSessionEventRequest {
  eventType: string;
  payloadJson: string;
  participantId?: string;
  roundNumber?: number;
}

export interface SessionEventDto {
  id: string;
  sessionId: string;
  eventType: string;
  payloadJson: string;
  participantId?: string;
  roundNumber?: number;
  createdAt: string;
}

export interface GetSessionEventsParams {
  type?: string;
  round?: number;
  limit?: number;
  cursor?: string;
}

export interface UserDicePresetDto {
  name: string;
  formula: string;
}

// ============================================================================
// Client Interface
// ============================================================================

export interface ToolkitClient {
  /** Add a session event (dice roll, score change, etc.) */
  addSessionEvent(sessionId: string, request: AddSessionEventRequest): Promise<SessionEventDto>;

  /** Get session events with optional filters */
  getSessionEvents(sessionId: string, params?: GetSessionEventsParams): Promise<SessionEventDto[]>;

  /** Get user dice presets for a toolkit */
  getUserDicePresets(toolkitId: string): Promise<UserDicePresetDto[]>;

  /** Add a custom dice preset */
  addUserDicePreset(
    toolkitId: string,
    preset: { name: string; formula: string }
  ): Promise<UserDicePresetDto>;

  /** Remove a custom dice preset by name */
  removeUserDicePreset(toolkitId: string, presetName: string): Promise<void>;
}

// ============================================================================
// Factory
// ============================================================================

export function createToolkitClient({ httpClient }: { httpClient: HttpClient }): ToolkitClient {
  return {
    async addSessionEvent(sessionId, request) {
      const response = await httpClient.post<SessionEventDto>(`/api/v1/session-events`, {
        sessionId,
        ...request,
      });
      return response;
    },

    async getSessionEvents(sessionId, params) {
      const searchParams = new URLSearchParams();
      if (params?.type) searchParams.set('type', params.type);
      if (params?.round != null) searchParams.set('round', String(params.round));
      if (params?.limit != null) searchParams.set('limit', String(params.limit));
      if (params?.cursor) searchParams.set('cursor', params.cursor);

      const qs = searchParams.toString();
      const path = `/api/v1/session-events/by-session/${sessionId}${qs ? `?${qs}` : ''}`;
      const response = await httpClient.get<SessionEventDto[]>(path);
      return response ?? [];
    },

    async getUserDicePresets(toolkitId) {
      const response = await httpClient.get<UserDicePresetDto[]>(
        `/api/v1/game-toolkits/${toolkitId}/dice-presets`
      );
      return response ?? [];
    },

    async addUserDicePreset(toolkitId, preset) {
      const response = await httpClient.post<UserDicePresetDto>(
        `/api/v1/game-toolkits/${toolkitId}/dice-presets`,
        preset
      );
      return response;
    },

    async removeUserDicePreset(toolkitId, presetName) {
      await httpClient.delete(
        `/api/v1/game-toolkits/${toolkitId}/dice-presets/${encodeURIComponent(presetName)}`
      );
    },
  };
}
