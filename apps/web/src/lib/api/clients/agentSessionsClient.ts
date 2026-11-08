/**
 * Agent Sessions Client
 * Issue #3375 - Agent Session Launch API Integration
 *
 * Client for session-based agent lifecycle management.
 */

import { z } from 'zod';

import type { HttpClient } from '../core/httpClient';

// ========== Request/Response Schemas ==========

export const LaunchSessionAgentRequestSchema = z.object({
  // Issue #4154: opzionale; assente, il backend usa l'agente di sistema (ADR-094). `agentId`, che il
  // backend ignorava, non c'è più.
  agentDefinitionId: z.string().uuid().optional(),
  gameId: z.string().uuid(),
  // C1 fix: default to '' so the BE uses GameState.Initial(UserId) instead of
  // GameState.FromJson('{}') which throws (ActivePlayer == Guid.Empty → 422).
  initialGameStateJson: z.string().optional().default(''),
});

export const LaunchSessionAgentResponseSchema = z.object({
  agentSessionId: z.string().uuid(),
});

export const EndSessionAgentRequestSchema = z.object({
  agentSessionId: z.string().uuid(),
});

// ========== Types ==========

export type LaunchSessionAgentRequest = z.infer<typeof LaunchSessionAgentRequestSchema>;
export type LaunchSessionAgentResponse = z.infer<typeof LaunchSessionAgentResponseSchema>;

export interface CreateAgentSessionsClientParams {
  httpClient: HttpClient;
}

/**
 * Agent Sessions API client
 */
export function createAgentSessionsClient({ httpClient }: CreateAgentSessionsClientParams) {
  return {
    /**
     * Launch a new agent session for a game session
     * POST /api/v1/game-sessions/{gameSessionId}/agent/launch
     *
     * Issue #3375: Agent Session Launch API Integration
     *
     * @param gameSessionId The game session ID
     * @param request Launch configuration
     */
    async launch(
      gameSessionId: string,
      request: LaunchSessionAgentRequest
    ): Promise<LaunchSessionAgentResponse> {
      const response = await httpClient.post<LaunchSessionAgentResponse>(
        `/api/v1/game-sessions/${encodeURIComponent(gameSessionId)}/agent/launch`,
        request,
        LaunchSessionAgentResponseSchema
      );

      if (!response) {
        throw new Error('Failed to launch agent session: no response from server');
      }

      return response;
    },

    /**
     * End an agent session
     * DELETE /api/v1/game-sessions/{gameSessionId}/agent
     *
     * Note: The agentSessionId is passed as a query parameter
     *
     * @param gameSessionId The game session ID
     * @param agentSessionId The agent session ID to end
     */
    async endSession(gameSessionId: string, agentSessionId: string): Promise<void> {
      await httpClient.delete(
        `/api/v1/game-sessions/${encodeURIComponent(gameSessionId)}/agent?agentSessionId=${encodeURIComponent(agentSessionId)}`
      );
    },
  };
}

export type AgentSessionsClient = ReturnType<typeof createAgentSessionsClient>;
