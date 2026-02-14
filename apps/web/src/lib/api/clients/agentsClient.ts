/**
 * Agents Client (Issue #868)
 *
 * Modular client for KnowledgeBase Agent operations.
 * Covers: Agent listing, invocation, configuration.
 */

import { z } from 'zod';

import {
  AgentDtoSchema,
  AgentResponseDtoSchema,
  GetAllAgentsResponseSchema,
  ConfigureAgentResponseSchema,
  ChessAgentResponseSchema,
  SetupGuideResponseSchema,
  AgentDocumentsDtoSchema,
  UpdateAgentDocumentsResponseSchema,
  PlayerModeSuggestionResponseSchema,
  typologySchema, // Issue #4126
  type AgentDto,
  type AgentResponseDto,
  type SSEEvent, // Issue #4126
  type InvokeAgentRequest,
  type CreateAgentRequest,
  type ConfigureAgentRequest,
  type ConfigureAgentResponse,
  type ChessAgentResponse,
  type SetupGuideResponse,
  type AgentDocumentsDto,
  type UpdateAgentDocumentsResponse,
  type PlayerModeSuggestionRequest,
  type PlayerModeSuggestionResponse,
  type Typology, // Added for AGT-012
} from '../schemas';

import type { HttpClient } from '../core/httpClient';

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

    /**
     * Get approved agent typologies (authenticated endpoint)
     * Issue #3186 (AGT-012): Agent Config Modal
     * @param status Filter by status (default: 'Approved')
     */
    async getTypologies(status: 'Approved' = 'Approved'): Promise<Typology[]> {
      const params = new URLSearchParams();
      params.append('status', status);

      const url = `/api/v1/agent-typologies?${params.toString()}`;
      const response = await httpClient.get<{
        success: boolean;
        typologies: Typology[];
        total: number;
      }>(url, z.object({
        success: z.boolean(),
        typologies: z.array(typologySchema),
        total: z.number(),
      }));

      return response?.typologies ?? [];
    },

    /**
     * Get recent agents for dashboard widget
     * Issue #4126: API Integration
     */
    async getRecent(limit: number = 10): Promise<AgentDto[]> {
      const response = await httpClient.get<AgentDto[]>(
        `/api/v1/agents/recent?limit=${limit}`,
        z.array(AgentDtoSchema)
      );
      return response ?? [];
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
     *
     * Issue #1977: Added ChessAgentResponseSchema validation
     */
    async invokeChess(request: {
      question: string;
      fenPosition?: string;
    }): Promise<ChessAgentResponse> {
      const response = await httpClient.post(
        '/api/v1/agents/chess',
        request,
        ChessAgentResponseSchema
      );
      if (!response) {
        throw new Error('Failed to invoke chess agent: no response from server');
      }
      return response;
    },

    /**
     * Generate setup guide (convenience wrapper)
     * POST /api/v1/agents/setup
     *
     * Issue #1977: Added SetupGuideResponseSchema validation
     */
    async generateSetupGuide(request: {
      gameId: string;
      chatId: string | null;
    }): Promise<SetupGuideResponse> {
      const response = await httpClient.post(
        '/api/v1/agents/setup',
        request,
        SetupGuideResponseSchema
      );
      if (!response) {
        throw new Error('Failed to generate setup guide: no response from server');
      }
      return response;
    },

    /**
     * Suggest player move (convenience wrapper)
     * POST /api/v1/agents/player-mode/suggest
     *
     * Issue #2421: Player Mode UI Controls
     */
    async suggestPlayerMove(
      request: PlayerModeSuggestionRequest
    ): Promise<PlayerModeSuggestionResponse> {
      const response = await httpClient.post(
        '/api/v1/agents/player-mode/suggest',
        request,
        PlayerModeSuggestionResponseSchema
      );
      if (!response) {
        throw new Error('Failed to suggest player move: no response from server');
      }
      return response;
    },

    // ========== Agent Documents (Issue #2399) ==========

    /**
     * Get selected documents for an agent's knowledge base
     * Implements GetAgentDocumentsQuery from backend
     * Issue #2399: Knowledge Base Document Selection
     * @param id Agent ID (GUID format)
     */
    async getDocuments(id: string): Promise<AgentDocumentsDto | null> {
      return httpClient.get(
        `/api/v1/agents/${encodeURIComponent(id)}/documents`,
        AgentDocumentsDtoSchema
      );
    },

    /**
     * Update selected documents for an agent's knowledge base (Admin only)
     * Implements UpdateAgentDocumentsCommand from backend
     * Issue #2399: Knowledge Base Document Selection
     * @param id Agent ID (GUID format)
     * @param documentIds Array of document IDs to select (max 50)
     */
    async updateDocuments(
      id: string,
      documentIds: string[]
    ): Promise<UpdateAgentDocumentsResponse> {
      const response = await httpClient.put<UpdateAgentDocumentsResponse>(
        `/api/v1/agents/${encodeURIComponent(id)}/documents`,
        { documentIds },
        UpdateAgentDocumentsResponseSchema
      );

      if (!response) {
        throw new Error('Failed to update agent documents: no response from server');
      }

      return response;
    },

    // ========== Agent Feedback (Issue #3352) ==========

    /**
     * Submit feedback for an AI response (thumbs up/down)
     * Implements ProvideAgentFeedbackCommand from backend
     * Issue #3352: AI Response Feedback System
     * @param request Feedback request with messageId, endpoint, outcome, and optional comment
     */
    async submitFeedback(request: {
      messageId: string;
      endpoint: string;
      userId: string;
      outcome: 'helpful' | 'not-helpful' | 'incorrect' | null;
      gameId?: string;
      comment?: string;
    }): Promise<{ ok: boolean }> {
      const response = await httpClient.post<{ ok: boolean }>(
        '/api/v1/ai/agents/feedback',
        {
          messageId: request.messageId,
          endpoint: request.endpoint,
          userId: request.userId,
          outcome: request.outcome,
          gameId: request.gameId,
          comment: request.comment,
        },
        z.object({ ok: z.boolean() })
      );

      if (!response) {
        throw new Error('Failed to submit feedback: no response from server');
      }

      return response;
    },

    // ========== Agent Chat SSE (Issue #4126) ==========

    /**
     * Chat with agent using SSE streaming
     * Returns async generator for streaming responses
     * Issue #4126: API Integration
     */
    async *chat(
      agentId: string,
      message: string,
      signal?: AbortSignal
    ): AsyncGenerator<SSEEvent, void, unknown> {
      const response = await fetch(`/api/v1/agents/${encodeURIComponent(agentId)}/chat`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ message }),
        signal,
      });

      if (!response.ok) {
        throw new Error(`Chat failed: ${response.statusText}`);
      }

      if (!response.body) {
        throw new Error('No response body');
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';

      try {
        while (true) {
          // Check if aborted before reading
          if (signal?.aborted) {
            break;
          }

          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n');
          buffer = lines.pop() || '';

          for (const line of lines) {
            if (line.startsWith('data: ')) {
              const data = line.slice(6);
              try {
                const event = JSON.parse(data);
                yield event as SSEEvent;
              } catch (e) {
                console.error('Failed to parse SSE event:', e);
              }
            }
          }
        }
      } catch (e) {
        // Clean exit on abort
        if (signal?.aborted) {
          return;
        }
        throw e;
      } finally {
        reader.releaseLock();
      }
    },
  };
}

export type AgentsClient = ReturnType<typeof createAgentsClient>;