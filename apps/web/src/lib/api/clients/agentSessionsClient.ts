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
  typologyId: z.string().uuid(),
  agentId: z.string().uuid(),
  gameId: z.string().uuid(),
  initialGameStateJson: z.string().optional().default('{}'),
});

export const LaunchSessionAgentResponseSchema = z.object({
  agentSessionId: z.string().uuid(),
});

export const UpdateAgentSessionConfigRequestSchema = z.object({
  agentSessionId: z.string().uuid(),
  modelType: z.string(),
  temperature: z.number().min(0).max(2),
  maxTokens: z.number().min(1).max(128000),
  ragStrategy: z.string(),
  ragParams: z.record(z.string(), z.unknown()).optional(),
});

export const EndSessionAgentRequestSchema = z.object({
  agentSessionId: z.string().uuid(),
});

// ========== Types ==========

export type LaunchSessionAgentRequest = z.infer<typeof LaunchSessionAgentRequestSchema>;
export type LaunchSessionAgentResponse = z.infer<typeof LaunchSessionAgentResponseSchema>;
export type UpdateAgentSessionConfigRequest = z.infer<typeof UpdateAgentSessionConfigRequestSchema>;

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
     * Update agent session configuration
     * PATCH /api/v1/game-sessions/{gameSessionId}/agent/config
     *
     * @param gameSessionId The game session ID
     * @param config Configuration update
     */
    async updateConfig(
      gameSessionId: string,
      config: UpdateAgentSessionConfigRequest
    ): Promise<void> {
      await httpClient.patch<void>(
        `/api/v1/game-sessions/${encodeURIComponent(gameSessionId)}/agent/config`,
        config,
        undefined
      );
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
