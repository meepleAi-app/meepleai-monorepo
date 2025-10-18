export const API_BASE_FALLBACK = "http://localhost:8080";

export const getApiBase = (): string => {
  const envBase = process.env.NEXT_PUBLIC_API_BASE?.trim();
  if (envBase && envBase !== "undefined" && envBase !== "null") {
    return envBase;
  }
  return API_BASE_FALLBACK;
};

// TypeScript types for session management (AUTH-05)
export interface SessionStatusResponse {
  expiresAt: string;
  lastSeenAt: string | null;
  remainingMinutes: number;
}

// TypeScript types for RuleSpec comments
export interface RuleSpecComment {
  id: string;
  gameId: string;
  version: string;
  atomId: string | null;
  userId: string;
  userDisplayName: string;
  commentText: string;
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
  const correlationId = response.headers.get('X-Correlation-Id') || undefined;
  let errorMessage = `API ${path} ${response.status}`;

  // Try to extract error message from response body
  try {
    const body = await response.json();
    if (body?.error) {
      errorMessage = body.error;
    }
  } catch {
    // If JSON parsing fails, use default message
  }

  return new ApiError(errorMessage, response.status, correlationId, response);
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
    async getComments(gameId: string, version: string): Promise<RuleSpecCommentsResponse | null> {
      return api.get<RuleSpecCommentsResponse>(
        `/api/v1/games/${gameId}/rulespec/versions/${version}/comments`
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

  // CHAT-05: Chat export API
  chat: {
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
    }
  }
};
