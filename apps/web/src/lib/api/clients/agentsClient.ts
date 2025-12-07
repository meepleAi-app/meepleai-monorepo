/**
 * Agents Client (Issue #868)
 *
 * Modular client for KnowledgeBase Agent operations.
 * Covers: Agent listing, invocation, configuration.
 */

import type { HttpClient } from '../core/httpClient';
import {
  AgentDtoSchema,
  AgentResponseDtoSchema,
  GetAllAgentsResponseSchema,
  ConfigureAgentResponseSchema,
  type AgentDto,
  type AgentResponseDto,
  type InvokeAgentRequest,
  type CreateAgentRequest,
  type ConfigureAgentRequest,
  type ConfigureAgentResponse,
} from '../schemas';

export interface CreateAgentsClientParams {
  httpClient: HttpClient;
}

/**
 * Agents API client with Zod validation
 */
export function createAgentsClient({ httpClient }: CreateAgentsClientParams) {
  return {
    // ========== Agent Queries ==========

    /**
     * Get all agents with optional filtering
     * Implements GetAllAgentsQuery from backend
     * @param activeOnly If true, only return active agents
     * @param type Optional agent type filter
     */
    async getAll(activeOnly?: boolean, type?: string): Promise<AgentDto[]> {
      const params = new URLSearchParams();
      if (activeOnly !== undefined) {
        params.append('activeOnly', activeOnly.toString());
      }
      if (type) {
        params.append('type', type);
      }

      const url = `/api/v1/agents${params.toString() ? `?${params.toString()}` : ''}`;
      const response = await httpClient.get<{
        success: boolean;
        agents: AgentDto[];
        count: number;
      }>(url, GetAllAgentsResponseSchema);

      return response?.agents ?? [];
    },

    /**
     * Get available agents (active only, convenience method)
     */
    async getAvailable(type?: string): Promise<AgentDto[]> {
      return this.getAll(true, type);
    },

    /**
     * Get agent by ID
     * Implements GetAgentByIdQuery from backend
     * @param id Agent ID (GUID format)
     */
    async getById(id: string): Promise<AgentDto | null> {
      return httpClient.get(`/api/v1/agents/${encodeURIComponent(id)}`, AgentDtoSchema);
    },

    // ========== Agent Commands ==========

    /**
     * Invoke an agent with a query
     * Implements InvokeAgentCommand from backend
     * @param id Agent ID (GUID format)
     * @param request Invocation request with query and optional context
     */
    async invoke(id: string, request: InvokeAgentRequest): Promise<AgentResponseDto> {
      const response = await httpClient.post<AgentResponseDto>(
        `/api/v1/agents/${encodeURIComponent(id)}/invoke`,
        request,
        AgentResponseDtoSchema
      );

      if (!response) {
        throw new Error('Failed to invoke agent: no response from server');
      }

      return response;
    },

    /**
     * Create a new agent (Admin only)
     * Implements CreateAgentCommand from backend
     * @param request Agent creation request
     */
    async create(request: CreateAgentRequest): Promise<AgentDto> {
      const response = await httpClient.post<AgentDto>('/api/v1/agents', request, AgentDtoSchema);

      if (!response) {
        throw new Error('Failed to create agent: no response from server');
      }

      return response;
    },

    /**
     * Configure an agent's strategy (Admin only)
     * Implements ConfigureAgentCommand from backend
     * @param id Agent ID (GUID format)
     * @param request Configuration request
     */
    async configure(id: string, request: ConfigureAgentRequest): Promise<ConfigureAgentResponse> {
      const response = await httpClient.put<ConfigureAgentResponse>(
        `/api/v1/agents/${encodeURIComponent(id)}/configure`,
        request,
        ConfigureAgentResponseSchema
      );

      if (!response) {
        throw new Error('Failed to configure agent: no response from server');
      }

      return response;
    },

    /**
     * Invoke chess agent (convenience wrapper)
     * POST /api/v1/agents/chess
     */
    async invokeChess(request: { question: string; fenPosition?: string }): Promise<any> {
      return httpClient.post('/api/v1/agents/chess', request);
    },

    /**
     * Generate setup guide (convenience wrapper)
     * POST /api/v1/agents/setup
     */
    async generateSetupGuide(request: { gameId: string; chatId: string | null }): Promise<any> {
      return httpClient.post('/api/v1/agents/setup', request);
    },
  };
}

export type AgentsClient = ReturnType<typeof createAgentsClient>;
