/**
 * Chat Sessions Client (Issue #3484)
 *
 * API client for ChatSession persistence endpoints.
 * Backend implementation: Issue #3483
 */

import {
  ChatSessionDtoSchema,
  ChatSessionListResponseSchema,
  ChatSessionTierLimitSchema,
  type AddChatSessionMessageRequest,
  type ChatSessionDto,
  type ChatSessionSummaryDto,
  type ChatSessionTierLimit,
  type CreateChatSessionRequest,
} from '../schemas/chat-sessions.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateChatSessionsClientParams {
  httpClient: HttpClient;
}

export interface ChatSessionsClient {
  /**
   * Create a new chat session
   */
  create(request: CreateChatSessionRequest): Promise<ChatSessionDto>;

  /**
   * Get a chat session by ID with all messages
   */
  getById(sessionId: string): Promise<ChatSessionDto | null>;

  /**
   * Get user's chat sessions for a specific game
   */
  getByUserAndGame(
    userId: string,
    gameId: string,
    options?: { page?: number; pageSize?: number }
  ): Promise<{ sessions: ChatSessionSummaryDto[]; totalCount: number }>;

  /**
   * Get user's recent chat sessions across all games
   */
  getRecent(
    userId: string,
    options?: { limit?: number }
  ): Promise<{ sessions: ChatSessionSummaryDto[]; totalCount: number }>;

  /**
   * Add a message to a chat session
   */
  addMessage(sessionId: string, request: Omit<AddChatSessionMessageRequest, 'sessionId'>): Promise<ChatSessionDto>;

  /**
   * Get the user's chat session tier limit and current usage
   * Issue #4913
   */
  getLimit(userId: string): Promise<ChatSessionTierLimit | null>;

  /**
   * Delete a chat session
   */
  delete(sessionId: string): Promise<void>;
}

/**
 * Create Chat Sessions API client with Zod validation
 */
export function createChatSessionsClient({
  httpClient,
}: CreateChatSessionsClientParams): ChatSessionsClient {
  const client: ChatSessionsClient = {
    async create(request: CreateChatSessionRequest): Promise<ChatSessionDto> {
      return httpClient.post('/api/v1/chat-sessions', request, ChatSessionDtoSchema);
    },

    async getById(sessionId: string): Promise<ChatSessionDto | null> {
      return httpClient.get(
        `/api/v1/chat-sessions/${encodeURIComponent(sessionId)}`,
        ChatSessionDtoSchema
      );
    },

    async getByUserAndGame(
      userId: string,
      gameId: string,
      options?: { page?: number; pageSize?: number }
    ): Promise<{ sessions: ChatSessionSummaryDto[]; totalCount: number }> {
      const params = new URLSearchParams();
      if (options?.page !== undefined) params.append('page', String(options.page));
      if (options?.pageSize !== undefined) params.append('pageSize', String(options.pageSize));

      const query = params.toString();
      const url = `/api/v1/chat-sessions/user/${encodeURIComponent(userId)}/game/${encodeURIComponent(gameId)}${query ? `?${query}` : ''}`;

      const response = await httpClient.get(url, ChatSessionListResponseSchema);
      return response ?? { sessions: [], totalCount: 0 };
    },

    async getRecent(
      userId: string,
      options?: { limit?: number }
    ): Promise<{ sessions: ChatSessionSummaryDto[]; totalCount: number }> {
      const params = new URLSearchParams();
      if (options?.limit !== undefined) params.append('limit', String(options.limit));

      const query = params.toString();
      const url = `/api/v1/chat-sessions/user/${encodeURIComponent(userId)}/recent${query ? `?${query}` : ''}`;

      const response = await httpClient.get(url, ChatSessionListResponseSchema);
      return response ?? { sessions: [], totalCount: 0 };
    },

    async addMessage(
      sessionId: string,
      request: Omit<AddChatSessionMessageRequest, 'sessionId'>
    ): Promise<ChatSessionDto> {
      return httpClient.post(
        `/api/v1/chat-sessions/${encodeURIComponent(sessionId)}/messages`,
        request,
        ChatSessionDtoSchema
      );
    },

    async getLimit(userId: string): Promise<ChatSessionTierLimit | null> {
      return httpClient.get(
        `/api/v1/users/${encodeURIComponent(userId)}/chat-sessions/limit`,
        ChatSessionTierLimitSchema
      );
    },

    async delete(sessionId: string): Promise<void> {
      return httpClient.delete(`/api/v1/chat-sessions/${encodeURIComponent(sessionId)}`);
    },
  };

  return client;
}
