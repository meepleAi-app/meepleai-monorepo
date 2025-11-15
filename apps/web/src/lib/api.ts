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

// AUTH-07: Two-Factor Authentication types
export interface TotpSetupResponse {
  secret: string;
  qrCodeUri: string;
  backupCodes: string[];
}

// SPRINT-1 Issue #1148: Updated to match backend DDD CQRS DTOs
export interface TwoFactorStatusDto {
  isEnabled: boolean;
  enabledAt: string | null;
  unusedBackupCodesCount: number;
}

export interface Enable2FAResult {
  success: boolean;
  backupCodes?: string[] | null;
  errorMessage?: string | null;
}

export interface Disable2FAResult {
  success: boolean;
  errorMessage?: string | null;
}

// Legacy type alias for backward compatibility (deprecated)
/** @deprecated Use TwoFactorStatusDto instead */
export type TwoFactorStatusResponse = TwoFactorStatusDto;

// SPRINT-2: Game Library types (Issue #854)
export interface Game {
  id: string;
  title: string;
  publisher: string | null;
  yearPublished: number | null;
  minPlayers: number | null;
  maxPlayers: number | null;
  minPlayTimeMinutes: number | null;
  maxPlayTimeMinutes: number | null;
  bggId: number | null;
  createdAt: string;
}

export interface GameFilters {
  search?: string;
  minPlayers?: number;
  maxPlayers?: number;
  minPlayTime?: number;
  maxPlayTime?: number;
  yearFrom?: number;
  yearTo?: number;
  bggOnly?: boolean;
}

export type GameSortField = 'title' | 'yearPublished' | 'minPlayers' | 'maxPlayers';
export type SortDirection = 'asc' | 'desc';

export interface GameSortOptions {
  field: GameSortField;
  direction: SortDirection;
}

export interface PaginatedGamesResponse {
  games: Game[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// SPRINT-4: Game Session types (Issue #863)
export interface SessionPlayerDto {
  playerName: string;
  playerOrder: number;
  color: string | null;
}

export interface GameSessionDto {
  id: string;
  gameId: string;
  status: string;
  startedAt: string;
  completedAt: string | null;
  playerCount: number;
  players: SessionPlayerDto[];
  winnerName: string | null;
  notes: string | null;
  durationMinutes: number;
}

// SPRINT-2: PDF Document types (Issue #855)
export interface PdfDocumentDto {
  id: string;
  gameId: string;
  fileName: string;
  filePath: string;
  fileSizeBytes: number;
  processingStatus: string; // Pending, Processing, Completed, Failed
  uploadedAt: string;
  processedAt: string | null;
  pageCount: number | null;
}

export interface StartSessionRequest {
  gameId: string;
  players: SessionPlayerDto[];
}

// SPRINT-1: Settings Pages types (Issue #848)
export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  role: string;
  createdAt: string;
  isTwoFactorEnabled: boolean;
  twoFactorEnabledAt: string | null;
}

export interface UserPreferences {
  language: string;
  emailNotifications: boolean;
  theme: 'light' | 'dark' | 'system';
  dataRetentionDays: number;
}

export interface UpdateProfileRequest {
  displayName?: string | null;
  email?: string | null;
}

export interface UpdatePreferencesRequest {
  language?: string;
  emailNotifications?: boolean;
  theme?: 'light' | 'dark' | 'system';
  dataRetentionDays?: number;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
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

// SPRINT-3: ChatThread DDD types (Issue #858, Backend #1126)
export interface ChatThreadDto {
  id: string;
  gameId: string | null;
  title: string | null;
  createdAt: string;
  lastMessageAt: string | null;
  messageCount: number;
  messages: ChatThreadMessageDto[];
}

export interface ChatThreadMessageDto {
  content: string;
  role: string;
  timestamp: string;
  // Optional metadata fields for ChatProvider compatibility (SPRINT-3 #858)
  backendMessageId?: string;
  endpoint?: string;
  gameId?: string;
  feedback?: 'helpful' | 'not-helpful' | null;
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

// CHAT-05: Chat export types
export type ExportFormat = "pdf" | "txt" | "md";

export interface ExportChatRequest {
  format: ExportFormat;
  dateFrom?: string;
  dateTo?: string;
}

// CONFIG-06: Dynamic Configuration System types
export interface SystemConfigurationDto {
  id: string;
  key: string;
  value: string;
  valueType: string;
  description: string | null;
  category: string;
  isActive: boolean;
  requiresRestart: boolean;
  environment: string;
  version: number;
  previousValue: string | null;
  createdAt: string;
  updatedAt: string;
  createdByUserId: string;
  updatedByUserId: string | null;
  lastToggledAt: string | null;
}

export interface CreateConfigurationRequest {
  key: string;
  value: string;
  valueType?: string;
  description?: string | null;
  category?: string;
  isActive?: boolean;
  requiresRestart?: boolean;
  environment?: string;
}

export interface UpdateConfigurationRequest {
  value?: string | null;
  valueType?: string | null;
  description?: string | null;
  category?: string | null;
  isActive?: boolean | null;
  requiresRestart?: boolean | null;
  environment?: string | null;
}

export interface ConfigurationHistoryDto {
  id: string;
  configurationId: string;
  key: string;
  oldValue: string;
  newValue: string;
  version: number;
  changedAt: string;
  changedByUserId: string;
  changeReason: string;
}

export interface BulkConfigurationUpdateRequest {
  updates: ConfigurationUpdate[];
}

export interface ConfigurationUpdate {
  id: string;
  value: string;
}

export interface ConfigurationValidationResult {
  isValid: boolean;
  errors: string[];
}

export interface ConfigurationExportDto {
  configurations: SystemConfigurationDto[];
  exportedAt: string;
  environment: string;
}

export interface ConfigurationImportRequest {
  configurations: CreateConfigurationRequest[];
  overwriteExisting?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

// EDIT-07: Bulk RuleSpec operations types
export interface BulkExportRequest {
  ruleSpecIds: string[];
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
    // Handle 204 No Content - don't attempt to parse JSON (P1 Badge fix)
    if (res.status === 204) {
      return undefined as T;
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

  // SPRINT-3: ChatThread API (Issue #858, Backend #1126)
  chatThreads: {
    /**
     * Get all chat threads for a specific game
     * @param gameId Game ID (GUID format)
     */
    async getByGame(gameId: string): Promise<ChatThreadDto[]> {
      const response = await api.get<ChatThreadDto[]>(
        `/api/v1/knowledge-base/chat-threads?gameId=${encodeURIComponent(gameId)}`
      );
      return response ?? [];
    },

    /**
     * Get a single chat thread by ID
     * @param threadId Thread ID (GUID format)
     */
    async getById(threadId: string): Promise<ChatThreadDto | null> {
      return api.get<ChatThreadDto>(
        `/api/v1/knowledge-base/chat-threads/${encodeURIComponent(threadId)}`
      );
    },

    /**
     * Create a new chat thread
     * @param request Thread creation request
     */
    async create(request: CreateChatThreadRequest): Promise<ChatThreadDto> {
      return api.post<ChatThreadDto>(
        '/api/v1/knowledge-base/chat-threads',
        request
      );
    },

    /**
     * Add a message to an existing thread
     * @param threadId Thread ID (GUID format)
     * @param request Message content and role
     */
    async addMessage(threadId: string, request: AddMessageRequest): Promise<ChatThreadDto> {
      return api.post<ChatThreadDto>(
        `/api/v1/knowledge-base/chat-threads/${encodeURIComponent(threadId)}/messages`,
        request
      );
    },

    /**
     * Close a chat thread (archive it)
     * @param threadId Thread ID (GUID format)
     */
    async close(threadId: string): Promise<ChatThreadDto> {
      return api.post<ChatThreadDto>(
        `/api/v1/knowledge-base/chat-threads/${encodeURIComponent(threadId)}/close`,
        {}
      );
    },

    /**
     * Reopen a closed thread
     * @param threadId Thread ID (GUID format)
     */
    async reopen(threadId: string): Promise<ChatThreadDto> {
      return api.post<ChatThreadDto>(
        `/api/v1/knowledge-base/chat-threads/${encodeURIComponent(threadId)}/reopen`,
        {}
      );
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
  },

  // CONFIG-06: Dynamic Configuration System API
  config: {
    /**
     * Get all configurations with optional filtering and pagination
     */
    async getConfigurations(
      category?: string,
      environment?: string,
      activeOnly: boolean = true,
      page: number = 1,
      pageSize: number = 50
    ): Promise<PagedResult<SystemConfigurationDto>> {
      const params = new URLSearchParams();
      if (category) params.append("category", category);
      if (environment) params.append("environment", environment);
      params.append("activeOnly", activeOnly.toString());
      params.append("page", page.toString());
      params.append("pageSize", pageSize.toString());

      const response = await api.get<PagedResult<SystemConfigurationDto>>(
        `/api/v1/admin/configurations?${params}`
      );
      if (!response) {
        throw new Error("Failed to fetch configurations");
      }
      return response;
    },

    /**
     * Get a single configuration by ID
     */
    async getConfiguration(id: string): Promise<SystemConfigurationDto> {
      const response = await api.get<SystemConfigurationDto>(`/api/v1/admin/configurations/${id}`);
      if (!response) {
        throw new Error(`Configuration ${id} not found`);
      }
      return response;
    },

    /**
     * Get a configuration by key
     */
    async getConfigurationByKey(key: string, environment?: string): Promise<SystemConfigurationDto> {
      const params = new URLSearchParams();
      if (environment) params.append("environment", environment);

      const response = await api.get<SystemConfigurationDto>(
        `/api/v1/admin/configurations/key/${encodeURIComponent(key)}?${params}`
      );
      if (!response) {
        throw new Error(`Configuration with key '${key}' not found`);
      }
      return response;
    },

    /**
     * Create a new configuration
     */
    async createConfiguration(request: CreateConfigurationRequest): Promise<SystemConfigurationDto> {
      return api.post<SystemConfigurationDto>("/api/v1/admin/configurations", request);
    },

    /**
     * Update an existing configuration
     */
    async updateConfiguration(
      id: string,
      request: UpdateConfigurationRequest
    ): Promise<SystemConfigurationDto> {
      return api.put<SystemConfigurationDto>(`/api/v1/admin/configurations/${id}`, request);
    },

    /**
     * Delete a configuration
     */
    async deleteConfiguration(id: string): Promise<void> {
      return api.delete(`/api/v1/admin/configurations/${id}`);
    },

    /**
     * Bulk update multiple configurations
     */
    async bulkUpdate(request: BulkConfigurationUpdateRequest): Promise<SystemConfigurationDto[]> {
      const response = await api.post<SystemConfigurationDto[]>(
        "/api/v1/admin/configurations/bulk-update",
        request
      );
      return response;
    },

    /**
     * Validate a configuration value
     */
    async validateConfiguration(
      key: string,
      value: string,
      valueType: string
    ): Promise<ConfigurationValidationResult> {
      const response = await api.post<ConfigurationValidationResult>(
        "/api/v1/admin/configurations/validate",
        { key, value, valueType }
      );
      return response;
    },

    /**
     * Export configurations for an environment
     */
    async exportConfigurations(
      environment: string,
      activeOnly: boolean = true
    ): Promise<ConfigurationExportDto> {
      const params = new URLSearchParams();
      params.append("environment", environment);
      params.append("activeOnly", activeOnly.toString());

      const response = await api.get<ConfigurationExportDto>(
        `/api/v1/admin/configurations/export?${params}`
      );
      if (!response) {
        throw new Error("Failed to export configurations");
      }
      return response;
    },

    /**
     * Import configurations from export file
     */
    async importConfigurations(request: ConfigurationImportRequest): Promise<number> {
      const response = await api.post<number>("/api/v1/admin/configurations/import", request);
      return response;
    },

    /**
     * Get configuration change history
     */
    async getHistory(configurationId: string, limit: number = 20): Promise<ConfigurationHistoryDto[]> {
      const response = await api.get<ConfigurationHistoryDto[]>(
        `/api/v1/admin/configurations/${configurationId}/history?limit=${limit}`
      );
      if (!response) {
        return [];
      }
      return response;
    },

    /**
     * Rollback configuration to a previous version
     */
    async rollback(
      configurationId: string,
      toVersion: number
    ): Promise<SystemConfigurationDto> {
      const response = await api.post<SystemConfigurationDto>(
        `/api/v1/admin/configurations/${configurationId}/rollback/${toVersion}`,
        {}
      );
      return response;
    },

    /**
     * Get all unique categories
     */
    async getCategories(): Promise<string[]> {
      const response = await api.get<string[]>("/api/v1/admin/configurations/categories");
      if (!response) {
        return [];
      }
      return response;
    },

    /**
     * Invalidate configuration cache
     */
    async invalidateCache(key?: string): Promise<void> {
      const body = key ? { key } : {};
      return api.post<void>("/api/v1/admin/configurations/cache/invalidate", body);
    }
  },

  // EDIT-07: Bulk RuleSpec operations API
  ruleSpecs: {
    /**
     * Export multiple rule specs as a ZIP file
     * @param ruleSpecIds Array of game IDs to export
     */
    async bulkExport(ruleSpecIds: string[]): Promise<void> {
      const res = await fetch(`${getApiBase()}/api/v1/rulespecs/bulk/export`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ruleSpecIds })
      });

      if (res.status === 401) {
        const correlationId = res.headers.get('X-Correlation-Id') || undefined;
        throw new ApiError('Unauthorized', 401, correlationId, res);
      }

      if (res.status === 403) {
        const correlationId = res.headers.get('X-Correlation-Id') || undefined;
        throw new ApiError('Forbidden - Editor or Admin role required', 403, correlationId, res);
      }

      if (!res.ok) {
        throw await createApiError(`/api/v1/rulespecs/bulk/export`, res);
      }

      // Extract filename from Content-Disposition header
      const contentDisposition = res.headers.get('Content-Disposition');
      let filename = `meepleai-rulespecs-${new Date().toISOString().split('T')[0]}.zip`;

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
  },

  // User profile API
  profile: {
    async get(): Promise<UserProfile | null> {
      return api.get<UserProfile>('/api/v1/users/profile');
    },

    async update(payload: UpdateProfileRequest): Promise<{ ok: boolean; message: string }> {
      return api.put<{ ok: boolean; message: string }>('/api/v1/users/profile', payload);
    },

    async changePassword(request: ChangePasswordRequest): Promise<{ ok: boolean; message: string }> {
      return api.put<{ ok: boolean; message: string }>('/api/v1/users/profile/password', request);
    }
  },

  // AUTH-07: Two-Factor Authentication API
  // SPRINT-1 Issue #1148: Updated to use backend DDD CQRS endpoints
  twoFactor: {
    async getStatus(): Promise<TwoFactorStatusDto> {
      return api.get<TwoFactorStatusDto>('/api/v1/users/me/2fa/status') as Promise<TwoFactorStatusDto>;
    },

    async setup(): Promise<TotpSetupResponse> {
      return api.post<TotpSetupResponse>('/api/v1/auth/2fa/setup');
    },

    async enable(code: string): Promise<Enable2FAResult> {
      const response = await api.post<{ message: string; backupCodes?: string[] }>('/api/v1/auth/2fa/enable', { code });
      // Backend returns 204 No Content or { message, backupCodes }
      // Guard against empty response (P1 Badge fix)
      return {
        success: true,
        backupCodes: response?.backupCodes || null,
        errorMessage: null
      };
    },

    async verify(code: string): Promise<void> {
      await api.post<void>('/api/v1/auth/2fa/verify', { code });
    },

    async disable(password: string, code: string): Promise<Disable2FAResult> {
      const response = await api.post<{ message: string }>('/api/v1/auth/2fa/disable', { password, code });
      // Backend returns { message } on success, or throws on error
      return {
        success: true,
        errorMessage: null
      };
    }
  },

  // SPRINT-2: Game Library API (Issue #854)
  games: {
    /**
     * Get all games with optional filtering, sorting, and pagination
     * @param filters Optional filters (search, players, playtime, year, BGG)
     * @param sort Optional sorting (field and direction)
     * @param page Page number (1-indexed)
     * @param pageSize Number of games per page
     */
    async getAll(
      filters?: GameFilters,
      sort?: GameSortOptions,
      page: number = 1,
      pageSize: number = 20
    ): Promise<PaginatedGamesResponse> {
      // For MVP: Fetch all games and do client-side filtering/sorting/pagination
      // Future: Add query params for server-side operations
      const allGames = await api.get<Game[]>('/api/v1/games');
      if (!allGames) {
        return {
          games: [],
          total: 0,
          page,
          pageSize,
          totalPages: 0
        };
      }

      // Client-side filtering
      let filtered = allGames;

      if (filters) {
        filtered = filtered.filter(game => {
          // Search filter (title or publisher)
          if (filters.search) {
            const searchLower = filters.search.toLowerCase();
            const titleMatch = game.title.toLowerCase().includes(searchLower);
            const publisherMatch = game.publisher?.toLowerCase().includes(searchLower) || false;
            if (!titleMatch && !publisherMatch) {
              return false;
            }
          }

          // Player count filters
          if (filters.minPlayers !== undefined && game.maxPlayers !== null && game.maxPlayers < filters.minPlayers) {
            return false;
          }
          if (filters.maxPlayers !== undefined && game.minPlayers !== null && game.minPlayers > filters.maxPlayers) {
            return false;
          }

          // Play time filters
          if (filters.minPlayTime !== undefined && game.maxPlayTimeMinutes !== null && game.maxPlayTimeMinutes < filters.minPlayTime) {
            return false;
          }
          if (filters.maxPlayTime !== undefined && game.minPlayTimeMinutes !== null && game.minPlayTimeMinutes > filters.maxPlayTime) {
            return false;
          }

          // Year filters
          if (filters.yearFrom !== undefined && game.yearPublished !== null && game.yearPublished < filters.yearFrom) {
            return false;
          }
          if (filters.yearTo !== undefined && game.yearPublished !== null && game.yearPublished > filters.yearTo) {
            return false;
          }

          // BGG-only filter
          if (filters.bggOnly && !game.bggId) {
            return false;
          }

          return true;
        });
      }

      // Client-side sorting
      if (sort) {
        filtered = [...filtered].sort((a, b) => {
          const aVal = a[sort.field];
          const bVal = b[sort.field];

          // Handle null values (push to end)
          if (aVal === null && bVal === null) return 0;
          if (aVal === null) return 1;
          if (bVal === null) return -1;

          // Sort by field
          let comparison = 0;
          if (sort.field === 'title') {
            comparison = aVal.toString().localeCompare(bVal.toString());
          } else {
            comparison = (aVal as number) - (bVal as number);
          }

          return sort.direction === 'asc' ? comparison : -comparison;
        });
      }

      // Client-side pagination
      const total = filtered.length;
      const totalPages = Math.ceil(total / pageSize);
      const startIndex = (page - 1) * pageSize;
      const endIndex = startIndex + pageSize;
      const paginatedGames = filtered.slice(startIndex, endIndex);

      return {
        games: paginatedGames,
        total,
        page,
        pageSize,
        totalPages
      };
    },

    /**
     * Get a single game by ID
     * @param id Game ID
     */
    async getById(id: string): Promise<Game | null> {
      return api.get<Game>(`/api/v1/games/${id}`);
    },

    /**
     * Get all sessions for a specific game (active + history)
     * @param gameId Game ID
     */
    async getSessions(gameId: string): Promise<GameSessionDto[]> {
      // Fetch both active sessions and history in parallel
      const [activeSessions, historyResponse] = await Promise.all([
        api.get<GameSessionDto[]>(`/api/v1/games/${gameId}/sessions/active`),
        api.get<PaginatedSessionsResponse>(`/api/v1/sessions/history?gameId=${gameId}&limit=1000`)
      ]);

      // Combine active and historical sessions
      const active = activeSessions ?? [];
      const history = historyResponse?.sessions ?? [];

      // Merge and remove duplicates (in case a session appears in both)
      const allSessions = [...active, ...history];
      const uniqueSessions = Array.from(
        new Map(allSessions.map(session => [session.id, session])).values()
      );

      return uniqueSessions;
    },

    /**
     * Get PDF documents for a specific game
     * @param gameId Game ID
     */
    async getDocuments(gameId: string): Promise<PdfDocumentDto[]> {
      const response = await api.get<PdfDocumentDto[]>(`/api/v1/games/${gameId}/documents`);
      return response ?? [];
    }
  },

  // SPRINT-4: Game Session Management API (Issue #1134)
  sessions: {
    /**
     * Get all active sessions with optional pagination
     * @param limit Maximum number of sessions to return (default: 20)
     * @param offset Number of sessions to skip (default: 0)
     */
    async getActive(limit: number = 20, offset: number = 0): Promise<PaginatedSessionsResponse> {
      const params = new URLSearchParams();
      params.append('limit', limit.toString());
      params.append('offset', offset.toString());

      const response = await api.get<PaginatedSessionsResponse>(
        `/api/v1/sessions/active?${params}`
      );
      if (!response) {
        return {
          sessions: [],
          total: 0,
          page: Math.floor(offset / limit) + 1,
          pageSize: limit
        };
      }
      return response;
    },

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

      const response = await api.get<PaginatedSessionsResponse>(
        `/api/v1/sessions/history?${params}`
      );
      if (!response) {
        const limit = filters?.limit || 20;
        const offset = filters?.offset || 0;
        return {
          sessions: [],
          total: 0,
          page: Math.floor(offset / limit) + 1,
          pageSize: limit
        };
      }
      return response;
    },

    /**
     * Get a single session by ID
     * @param id Session ID
     */
    async getById(id: string): Promise<GameSessionDto | null> {
      return api.get<GameSessionDto>(`/api/v1/sessions/${id}`);
    },

    /**
     * Start a new game session
     * @param request Session start request with game ID and players
     */
    async start(request: StartSessionRequest): Promise<GameSessionDto> {
      return api.post<GameSessionDto>('/api/v1/sessions', request);
    },

    /**
     * Pause an active session
     * @param id Session ID
     */
    async pause(id: string): Promise<GameSessionDto> {
      return api.post<GameSessionDto>(`/api/v1/sessions/${id}/pause`);
    },

    /**
     * Resume a paused session
     * @param id Session ID
     */
    async resume(id: string): Promise<GameSessionDto> {
      return api.post<GameSessionDto>(`/api/v1/sessions/${id}/resume`);
    },

    /**
     * End a session without marking it complete
     * @param id Session ID
     * @param winnerName Optional winner name
     */
    async end(id: string, winnerName?: string | null): Promise<GameSessionDto> {
      return api.post<GameSessionDto>(`/api/v1/sessions/${id}/end`, { winnerName });
    },

    /**
     * Complete a session with winner information
     * @param id Session ID
     * @param request Completion request with optional winner name
     */
    async complete(id: string, request?: CompleteSessionRequest): Promise<GameSessionDto> {
      return api.post<GameSessionDto>(`/api/v1/sessions/${id}/complete`, request || {});
    },

    /**
     * Abandon a session
     * @param id Session ID
     */
    async abandon(id: string): Promise<GameSessionDto> {
      return api.post<GameSessionDto>(`/api/v1/sessions/${id}/abandon`);
    }
  }
};

// Issue #858: Removed duplicate SessionPlayerDto, GameSessionDto, StartSessionRequest
// These are already defined at lines 146-167

export interface CompleteSessionRequest {
  winnerName?: string | null;
}

export interface SessionHistoryFilters {
  gameId?: string;
  startDate?: string;
  endDate?: string;
  limit?: number;
  offset?: number;
}

export interface PaginatedSessionsResponse {
  sessions: GameSessionDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface SessionStatistics {
  totalSessions: number;
  completedSessions: number;
  abandonedSessions: number;
  averageDurationMinutes: number;
  winRates: { [playerName: string]: number };
}
