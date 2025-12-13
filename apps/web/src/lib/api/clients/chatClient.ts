/**
 * Chat Client (FE-IMP-005)
 *
 * Modular client for KnowledgeBase bounded context (Chat).
 * Covers: ChatThreads, Messages, RuleSpec Comments, Cache Management, Export
 */

import type { FeedbackOutcome } from '@/lib/constants/feedback';
import type { HttpClient } from '../core/httpClient';
import { downloadFile, getApiBase } from '../core/httpClient';
import {
  ChatThreadDtoSchema,
  ChatMessageResponseSchema,
  RuleSpecCommentsResponseSchema,
  RuleSpecCommentSchema,
  CacheStatsSchema,
  type ChatThreadDto,
  type ChatMessageResponse,
  type RuleSpecCommentsResponse,
  type RuleSpecComment,
  type CacheStats,
} from '../schemas';

export interface CreateChatClientParams {
  httpClient: HttpClient;
}

export interface CreateChatThreadRequest {
  gameId?: string | null;
  title?: string | null;
  initialMessage?: string | null;
}

export interface AddMessageRequest {
  content: string;
  role: string;
}

export interface CreateRuleSpecCommentRequest {
  atomId: string | null;
  lineNumber?: number | null;
  commentText: string;
}

export interface UpdateRuleSpecCommentRequest {
  commentText: string;
}

export interface CreateReplyRequest {
  commentText: string;
}

export type ExportFormat = 'pdf' | 'txt' | 'md';

export interface ExportChatRequest {
  format: ExportFormat;
  dateFrom?: string;
  dateTo?: string;
}

export interface BulkExportRequest {
  ruleSpecIds: string[];
}

export interface ChatClient {
  // Chat Threads
  getThreadsByGame(gameId: string): Promise<ChatThreadDto[]>;
  getThreadById(threadId: string): Promise<ChatThreadDto | null>;
  createThread(request: CreateChatThreadRequest): Promise<ChatThreadDto>;
  addMessage(threadId: string, request: AddMessageRequest): Promise<ChatThreadDto>;
  closeThread(threadId: string): Promise<ChatThreadDto>;
  reopenThread(threadId: string): Promise<ChatThreadDto>;
  deleteThread(threadId: string): Promise<void>;

  // Chat Messages
  updateMessage(chatId: string, messageId: string, content: string): Promise<ChatMessageResponse>;
  deleteMessage(chatId: string, messageId: string): Promise<void>;

  // Chat Export
  exportChat(chatId: string, request: ExportChatRequest): Promise<void>;

  // RuleSpec Comments
  getRuleSpecComments(
    gameId: string,
    version: string,
    includeResolved?: boolean
  ): Promise<RuleSpecCommentsResponse | null>;
  createRuleSpecComment(
    gameId: string,
    version: string,
    request: CreateRuleSpecCommentRequest
  ): Promise<RuleSpecComment>;
  updateRuleSpecComment(
    gameId: string,
    commentId: string,
    request: UpdateRuleSpecCommentRequest
  ): Promise<RuleSpecComment>;
  deleteRuleSpecComment(gameId: string, commentId: string): Promise<void>;
  createCommentReply(
    parentCommentId: string,
    request: CreateReplyRequest
  ): Promise<RuleSpecComment>;
  resolveComment(commentId: string): Promise<void>;
  unresolveComment(commentId: string): Promise<void>;

  // Bulk Operations
  bulkExportRuleSpecs(request: BulkExportRequest): Promise<void>;

  // Cache Management
  getCacheStats(gameId?: string): Promise<CacheStats | null>;
  invalidateGameCache(gameId: string): Promise<void>;
  invalidateCacheByTag(tag: string): Promise<void>;

  // Agent Feedback
  submitAgentFeedback(request: {
    messageId: string;
    endpoint: string;
    gameId: string;
    outcome: FeedbackOutcome;
  }): Promise<void>;
}

/**
 * Create Chat API client with Zod validation
 */
export function createChatClient({ httpClient }: CreateChatClientParams): ChatClient {
  const client: ChatClient = {
    // ========== Chat Threads ==========

    async getThreadsByGame(gameId: string): Promise<ChatThreadDto[]> {
      const response = await httpClient.get<ChatThreadDto[]>(
        `/api/v1/chat-threads?gameId=${encodeURIComponent(gameId)}`
      );
      return response ?? [];
    },

    async getThreadById(threadId: string): Promise<ChatThreadDto | null> {
      return httpClient.get(
        `/api/v1/chat-threads/${encodeURIComponent(threadId)}`,
        ChatThreadDtoSchema
      );
    },

    async createThread(request: CreateChatThreadRequest): Promise<ChatThreadDto> {
      return httpClient.post('/api/v1/chat-threads', request, ChatThreadDtoSchema);
    },

    async addMessage(threadId: string, request: AddMessageRequest): Promise<ChatThreadDto> {
      return httpClient.post(
        `/api/v1/chat-threads/${encodeURIComponent(threadId)}/messages`,
        request,
        ChatThreadDtoSchema
      );
    },

    async closeThread(threadId: string): Promise<ChatThreadDto> {
      return httpClient.post(
        `/api/v1/chat-threads/${encodeURIComponent(threadId)}/close`,
        {},
        ChatThreadDtoSchema
      );
    },

    async reopenThread(threadId: string): Promise<ChatThreadDto> {
      return httpClient.post(
        `/api/v1/chat-threads/${encodeURIComponent(threadId)}/reopen`,
        {},
        ChatThreadDtoSchema
      );
    },

    async deleteThread(threadId: string): Promise<void> {
      return httpClient.delete(`/api/v1/chat-threads/${threadId}`);
    },

    // ========== Chat Messages ==========

    async updateMessage(
      chatId: string,
      messageId: string,
      content: string
    ): Promise<ChatMessageResponse> {
      return httpClient.put(
        `/api/v1/chat-threads/${chatId}/messages/${messageId}`,
        { content },
        ChatMessageResponseSchema
      );
    },

    async deleteMessage(chatId: string, messageId: string): Promise<void> {
      return httpClient.delete(`/api/v1/chat-threads/${chatId}/messages/${messageId}`);
    },

    // ========== Chat Export ==========

    async exportChat(chatId: string, request: ExportChatRequest): Promise<void> {
      const baseUrl = getApiBase();
      const params = new URLSearchParams();
      if (request.format) params.append('format', request.format);
      if (request.dateFrom) params.append('dateFrom', request.dateFrom);
      if (request.dateTo) params.append('dateTo', request.dateTo);
      const query = params.toString();
      const url = `${baseUrl}/api/v1/chat-threads/${encodeURIComponent(chatId)}/export${
        query ? `?${query}` : ''
      }`;

      const response = await fetch(url, {
        method: 'GET',
        credentials: 'include',
      });
      if (!response.ok) {
        throw new Error(`Failed to export chat: ${response.status}`);
      }
      const blob = await response.blob();
      const disposition = response.headers.get('content-disposition');
      const filenameMatch = disposition?.match(/filename="?([^";]+)"?/i);
      const filename = filenameMatch?.[1] ?? `chat-${chatId}.zip`;
      downloadFile(blob, filename);
    },

    // ========== RuleSpec Comments ==========

    async getRuleSpecComments(
      gameId: string,
      version: string,
      includeResolved: boolean = true
    ): Promise<RuleSpecCommentsResponse | null> {
      const resolvedParam = includeResolved ? 'true' : 'false';
      return httpClient.get(
        `/api/v1/rulespecs/${encodeURIComponent(gameId)}/${encodeURIComponent(
          version
        )}/comments?includeResolved=${resolvedParam}`,
        RuleSpecCommentsResponseSchema
      );
    },

    async createRuleSpecComment(
      gameId: string,
      version: string,
      request: CreateRuleSpecCommentRequest
    ): Promise<RuleSpecComment> {
      return httpClient.post(
        `/api/v1/rulespecs/${encodeURIComponent(gameId)}/${encodeURIComponent(version)}/comments`,
        request,
        RuleSpecCommentSchema
      );
    },

    async updateRuleSpecComment(
      gameId: string,
      commentId: string,
      request: UpdateRuleSpecCommentRequest
    ): Promise<RuleSpecComment> {
      return httpClient.put(
        `/api/v1/comments/${encodeURIComponent(commentId)}`,
        request,
        RuleSpecCommentSchema
      );
    },

    async deleteRuleSpecComment(gameId: string, commentId: string): Promise<void> {
      return httpClient.delete(`/api/v1/comments/${encodeURIComponent(commentId)}`);
    },

    async createCommentReply(
      parentCommentId: string,
      request: CreateReplyRequest
    ): Promise<RuleSpecComment> {
      return httpClient.post(
        `/api/v1/comments/${parentCommentId}/replies`,
        request,
        RuleSpecCommentSchema
      );
    },

    async resolveComment(commentId: string): Promise<void> {
      return httpClient.post(`/api/v1/comments/${commentId}/resolve`, {});
    },

    async unresolveComment(commentId: string): Promise<void> {
      return httpClient.post(`/api/v1/comments/${commentId}/unresolve`, {});
    },

    // ========== Bulk Operations ==========

    async bulkExportRuleSpecs(request: BulkExportRequest): Promise<void> {
      const { blob, filename } = await httpClient.postFile(
        '/api/v1/rulespecs/bulk/export',
        request
      );
      downloadFile(blob, filename);
    },

    // ========== Cache Management ==========

    async getCacheStats(gameId?: string): Promise<CacheStats | null> {
      const path = gameId
        ? `/api/v1/admin/cache/stats?gameId=${encodeURIComponent(gameId)}`
        : '/api/v1/admin/cache/stats';
      return httpClient.get(path, CacheStatsSchema);
    },

    async invalidateGameCache(gameId: string): Promise<void> {
      return httpClient.delete(`/api/v1/admin/cache/games/${encodeURIComponent(gameId)}`);
    },

    async invalidateCacheByTag(tag: string): Promise<void> {
      return httpClient.delete(`/api/v1/admin/cache/tags/${encodeURIComponent(tag)}`);
    },

    // ========== Agent Feedback ==========

    async submitAgentFeedback(request: {
      messageId: string;
      endpoint: string;
      gameId: string;
      outcome: FeedbackOutcome;
    }): Promise<void> {
      return httpClient.post('/api/v1/agents/feedback', request);
    },
  };

  return client;
}
