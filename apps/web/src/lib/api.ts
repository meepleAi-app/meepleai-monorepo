export const API_BASE_FALLBACK = "http://localhost:8080";

export const getApiBase = (): string => {
  const envBase = process.env.NEXT_PUBLIC_API_BASE?.trim();
  if (envBase && envBase !== "undefined" && envBase !== "null") {
    return envBase;
  }
  return API_BASE_FALLBACK;
};

// TypeScript types for user search (EDIT-05)
export interface UserSearchResult {
  id: string;
  displayName: string;
  email: string;
}

// TypeScript types for session management (AUTH-05)
export interface SessionStatusResponse {
  expiresAt: string;
  lastSeenAt: string | null;
  remainingMinutes: number;
}

// TypeScript types for RuleSpec comments (EDIT-05: Enhanced with threading, resolution, mentions)
export interface RuleSpecComment {
  id: string;
  gameId: string;
  version: string;
  atomId: string | null;
  // EDIT-05: Inline annotations
  lineNumber: number | null;
  lineContext: string | null;
  // EDIT-05: Threading
  parentCommentId: string | null;
  replies: RuleSpecComment[];
  // Original fields
  userId: string;
  userDisplayName: string;
  commentText: string;
  // EDIT-05: Resolution tracking
  isResolved: boolean;
  resolvedByUserId: string | null;
  resolvedByDisplayName: string | null;
  resolvedAt: string | null;
  // EDIT-05: User mentions
  mentionedUserIds: string[];
  // Timestamps
  createdAt: string;
  updatedAt: string | null;
}

export interface RuleSpecCommentsResponse {
  gameId: string;
  version: string;
  comments: RuleSpecComment[];
  totalComments: number;
}

export interface CreateRuleSpecCommentRequest {
  atomId: string | null;
  lineNumber?: number | null; // EDIT-05: Optional line number for inline comments
  commentText: string;
}

// EDIT-05: Request for creating threaded replies
export interface CreateReplyRequest {
  commentText: string;
}

export interface UpdateRuleSpecCommentRequest {
  commentText: string;
}

// AUTH-05: Session status response
export interface SessionStatusResponse {
  expiresAt: string;
  lastSeenAt: string | null;
  remainingMinutes: number;
}

// AI-13: BoardGameGeek API types
export interface BggSearchResult {
  bggId: number;
  name: string;
  yearPublished: number | null;
  thumbnailUrl: string | null;
  type: string; // "boardgame", "boardgameexpansion", etc.
}

export interface BggSearchResponse {
  results: BggSearchResult[];
}

export interface BggGameDetails {
  bggId: number;
  name: string;
  description: string | null;
  yearPublished: number | null;
  minPlayers: number | null;
  maxPlayers: number | null;
  playingTime: number | null;
  minPlayTime: number | null;
  maxPlayTime: number | null;
  minAge: number | null;
  averageRating: number | null;
  bayesAverageRating: number | null;
  usersRated: number | null;
  averageWeight: number | null; // Complexity: 1-5
  thumbnailUrl: string | null;
  imageUrl: string | null;
  categories: string[];
  mechanics: string[];
  designers: string[];
  publishers: string[];
}

// PERF-03: Cache statistics types
export interface TopQuestion {
  questionHash: string;
  hitCount: number;
  missCount: number;
  lastHitAt: string | null;
}

export interface CacheStats {
  totalHits: number;
  totalMisses: number;
  hitRate: number;
  totalKeys: number;
  cacheSizeBytes: number;
  topQuestions: TopQuestion[];
}

// CHAT-06: Chat message response type
export interface ChatMessageResponse {
  id: string;
  chatId: string;
  userId: string | null;
  level: string;
  content: string;
  sequenceNumber: number;
  createdAt: string;
  updatedAt: string | null;
  isDeleted: boolean;
  deletedAt: string | null;
  deletedByUserId: string | null;
  isInvalidated: boolean;
  metadataJson: string | null;
}

// CHAT-05: Chat export types
export type ExportFormat = "pdf" | "txt" | "md";

export interface ExportChatRequest {
  format: ExportFormat;
  dateFrom?: string;
  dateTo?: string;
}

// Enhanced error class with correlation ID (PDF-06)
export class ApiError extends Error {
  constructor(
    message: string,
    public statusCode?: number,
    public correlationId?: string,
    public response?: Response
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/**
 * Creates an enhanced error with correlation ID from response
 */
async function createApiError(path: string, response: Response): Promise<ApiError> {
  const correlationId = typeof response.headers?.get === 'function'
    ? response.headers.get('X-Correlation-Id') || undefined
    : undefined;
  const status = typeof response.status === 'number' ? response.status : 500;
  let errorMessage = `API ${path} ${status}`;

  // Try to extract error message from response body
  try {
    const body = await response.json();
    if (body?.error) {
      errorMessage = body.error;
    }
  } catch {
    // If JSON parsing fails, use default message
  }

  return new ApiError(errorMessage, status, correlationId, response);
}

export const api = {
  async get<T>(path: string): Promise<T | null> {
    const res = await fetch(`${getApiBase()}${path}`, {
      method: "GET",
      credentials: "include"
    });
    if (res.status === 401) {
      return null;
    }
    if (!res.ok) {
      throw await createApiError(path, res);
    }
    return res.json();
  },
  async post<T>(path: string, body?: unknown): Promise<T> {
    const res = await fetch(`${getApiBase()}${path}`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body ?? {})
    });
    if (res.status === 401) {
      const correlationId = res.headers.get('X-Correlation-Id') || undefined;
      throw new ApiError("Unauthorized", 401, correlationId, res);
    }
    if (!res.ok) {
      throw await createApiError(path, res);
    }
    return res.json();
  },
  async put<T>(path: string, body: unknown): Promise<T> {
    const res = await fetch(`${getApiBase()}${path}`, {
      method: "PUT",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    if (res.status === 401) {
      const correlationId = res.headers.get('X-Correlation-Id') || undefined;
      throw new ApiError("Unauthorized", 401, correlationId, res);
    }
    if (!res.ok) {
      throw await createApiError(path, res);
    }
    return res.json();
  },
  async delete(path: string): Promise<void> {
    const res = await fetch(`${getApiBase()}${path}`, {
      method: "DELETE",
      credentials: "include"
    });
    if (res.status === 401) {
      const correlationId = res.headers.get('X-Correlation-Id') || undefined;
      throw new ApiError("Unauthorized", 401, correlationId, res);
    }
    if (!res.ok) {
      throw await createApiError(path, res);
    }
    // DELETE returns 204 NoContent, no body to parse
  },

  // AUTH-05: Session management API
  auth: {
    async getSessionStatus(): Promise<SessionStatusResponse | null> {
      return api.get<SessionStatusResponse>('/api/v1/auth/session/status');
    },

    async extendSession(): Promise<SessionStatusResponse> {
      return api.post<SessionStatusResponse>('/api/v1/auth/session/extend');
    }
  },

  // RuleSpec Comment API
  ruleSpecComments: {
    async getComments(
      gameId: string,
      version: string,
      includeResolved: boolean = true
    ): Promise<RuleSpecCommentsResponse | null> {
      const resolvedParam = includeResolved ? 'true' : 'false';
      return api.get<RuleSpecCommentsResponse>(
        `/api/v1/games/${gameId}/rulespec/versions/${version}/comments?includeResolved=${resolvedParam}`
      );
    },

    async createComment(
      gameId: string,
      version: string,
      request: CreateRuleSpecCommentRequest
    ): Promise<RuleSpecComment> {
      return api.post<RuleSpecComment>(
        `/api/v1/games/${gameId}/rulespec/versions/${version}/comments`,
        request
      );
    },

    async updateComment(
      gameId: string,
      commentId: string,
      request: UpdateRuleSpecCommentRequest
    ): Promise<RuleSpecComment> {
      return api.put<RuleSpecComment>(
        `/api/v1/games/${gameId}/rulespec/comments/${commentId}`,
        request
      );
    },

    async deleteComment(gameId: string, commentId: string): Promise<void> {
      return api.delete(`/api/v1/games/${gameId}/rulespec/comments/${commentId}`);
    },

    // EDIT-05: Threaded reply support
    async createReply(
      parentCommentId: string,
      request: CreateReplyRequest
    ): Promise<RuleSpecComment> {
      return api.post<RuleSpecComment>(
        `/api/v1/rulespec/comments/${parentCommentId}/replies`,
        request
      );
    },

    // EDIT-05: Resolution management
    async resolveComment(commentId: string): Promise<void> {
      return api.post(`/api/v1/rulespec/comments/${commentId}/resolve`);
    },

    async unresolveComment(commentId: string): Promise<void> {
      return api.post(`/api/v1/rulespec/comments/${commentId}/unresolve`);
    }
  },

  // PERF-03: Cache management API
  cache: {
    async getStats(gameId?: string): Promise<CacheStats | null> {
      const path = gameId
        ? `/api/v1/admin/cache/stats?gameId=${encodeURIComponent(gameId)}`
        : `/api/v1/admin/cache/stats`;
      return api.get<CacheStats>(path);
    },

    async invalidateGameCache(gameId: string): Promise<void> {
      return api.delete(`/api/v1/admin/cache/games/${encodeURIComponent(gameId)}`);
    },

    async invalidateByTag(tag: string): Promise<void> {
      return api.delete(`/api/v1/admin/cache/tags/${encodeURIComponent(tag)}`);
    }
  },

  // PDF-08: PDF processing progress API
  pdf: {
    async getProcessingProgress(pdfId: string): Promise<import('../types/pdf').ProcessingProgress | null> {
      return api.get<import('../types/pdf').ProcessingProgress>(
        `/api/v1/pdfs/${encodeURIComponent(pdfId)}/progress`
      );
    },

    async cancelProcessing(pdfId: string): Promise<void> {
      return api.delete(`/api/v1/pdfs/${encodeURIComponent(pdfId)}/processing`);
    }
  },

  // CHAT-05 + CHAT-06: Chat API (export, message management)
  chat: {
    // CHAT-05: Export chat functionality
    async exportChat(chatId: string, request: ExportChatRequest): Promise<void> {
      const res = await fetch(`${getApiBase()}/api/v1/chats/${encodeURIComponent(chatId)}/export`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
      });

      if (res.status === 401) {
        const correlationId = res.headers.get('X-Correlation-Id') || undefined;
        throw new ApiError('Unauthorized', 401, correlationId, res);
      }

      if (!res.ok) {
        throw await createApiError(`/api/v1/chats/${chatId}/export`, res);
      }

      // Extract filename from Content-Disposition header
      const contentDisposition = res.headers.get('Content-Disposition');
      let filename = `chat-${chatId}-export.${request.format}`;

      if (contentDisposition) {
        const filenameMatch = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
        if (filenameMatch && filenameMatch[1]) {
          filename = filenameMatch[1].replace(/['"]/g, '');
        }
      }

      // Convert response to blob
      const blob = await res.blob();

      // Create download link and trigger download
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = filename;
      document.body.appendChild(link);
      link.click();

      // Clean up
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
    },

    // CHAT-06: Message management functionality
    async updateMessage(
      chatId: string,
      messageId: string,
      content: string
    ): Promise<ChatMessageResponse> {
      return api.put<ChatMessageResponse>(
        `/api/v1/chats/${chatId}/messages/${messageId}`,
        { content }
      );
    },

    async deleteMessage(chatId: string, messageId: string): Promise<void> {
      return api.delete(`/api/v1/chats/${chatId}/messages/${messageId}`);
    }
  },

  // AI-13: BoardGameGeek API integration
  bgg: {
    async search(query: string, exact: boolean = false): Promise<BggSearchResponse> {
      const params = new URLSearchParams({ q: query });
      if (exact) {
        params.append("exact", "true");
      }
      const response = await api.get<BggSearchResponse>(`/api/v1/bgg/search?${params}`);
      if (!response) {
        throw new Error("Failed to search BoardGameGeek");
      }
      return response;
    },

    async getGameDetails(bggId: number): Promise<BggGameDetails> {
      const response = await api.get<BggGameDetails>(`/api/v1/bgg/games/${bggId}`);
      if (!response) {
        throw new Error(`Game with BGG ID ${bggId} not found`);
      }
      return response;
    }
  }
};
