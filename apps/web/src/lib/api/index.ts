/**
 * Modular API SDK (FE-IMP-005)
 *
 * Factory for creating API client with dependency injection support.
 * Provides modular feature clients with Zod validation and centralized error handling.
 *
 * @example
 * ```typescript
 * // Default usage
 * const api = createApiClient();
 *
 * // With custom fetch for testing
 * const api = createApiClient({ fetchImpl: mockFetch });
 *
 * // Usage
 * const profile = await api.auth.getProfile();
 * const games = await api.games.getAll();
 * const session = await api.sessions.start({ gameId, players });
 * ```
 */

import { HttpClient, type HttpClientConfig } from './core/httpClient';
import {
  createAuthClient,
  createGamesClient,
  createSessionsClient,
  createChatClient,
  createPdfClient,
  createConfigClient,
  createBggClient,
  createAgentsClient,
  type AuthClient,
  type GamesClient,
  type SessionsClient,
  type ChatClient,
  type PdfClient,
  type ConfigClient,
  type BggClient,
  type AgentsClient,
} from './clients';

// Re-export client-specific types for consumer convenience
export type {
  ExportFormat,
  CreateChatThreadRequest,
  AddMessageRequest,
  CreateRuleSpecCommentRequest,
  UpdateRuleSpecCommentRequest,
  CreateReplyRequest,
  ExportChatRequest,
  BulkExportRequest,
} from './clients/chatClient';

export type {
  GameFilters,
  GameSortOptions,
  GameSortField,
  SortDirection,
} from './clients/gamesClient';

export type {
  SessionHistoryFilters,
} from './clients/sessionsClient';

export type {
  CreateConfigurationRequest,
  UpdateConfigurationRequest,
  BulkConfigurationUpdateRequest,
  ConfigurationImportRequest,
} from './clients/configClient';

// Re-export core utilities for consumer convenience
export { getApiBase } from './core/httpClient';
export {
  ApiError,
  UnauthorizedError,
  ForbiddenError,
  NotFoundError,
  ValidationError,
  RateLimitError,
  ServerError,
  NetworkError,
  SchemaValidationError,
} from './core/errors';
export { logger, logApiError } from './core/logger';

// Re-export PDF-specific types
export type { ProcessingProgress } from './schemas/pdf.schemas';

// Re-export all types and schemas
export * from './schemas';
export * from './types';

/**
 * Configuration for API client factory
 */
export interface ApiClientConfig {
  /**
   * Base URL for API (defaults to NEXT_PUBLIC_API_BASE or localhost:8080)
   */
  baseUrl?: string;

  /**
   * Custom fetch implementation (useful for testing)
   */
  fetchImpl?: typeof fetch;
}

/**
 * Modular API client with feature-specific sub-clients
 */
export interface ApiClient {
  /** Authentication & User Management */
  auth: AuthClient;

  /** Games CRUD & BoardGameGeek integration */
  games: GamesClient;

  /** Game Sessions Management */
  sessions: SessionsClient;

  /** Chat, RuleSpec Comments, Cache Management */
  chat: ChatClient;

  /** PDF Processing & Document Management */
  pdf: PdfClient;

  /** System Configuration & Feature Flags */
  config: ConfigClient;

  /** BoardGameGeek API integration */
  bgg: BggClient;

  /** AI Agents Management & Invocation (Issue #868) */
  agents: AgentsClient;

  /**
   * @deprecated Use feature-specific clients instead (auth, games, sessions, etc.)
   * Direct HTTP methods for endpoints not yet migrated to modular clients
   */
  get<T>(path: string): Promise<T | null>;

  /**
   * @deprecated Use feature-specific clients instead (auth, games, sessions, etc.)
   * Direct HTTP methods for endpoints not yet migrated to modular clients
   */
  post<T>(path: string, body?: unknown): Promise<T>;

  /**
   * @deprecated Use feature-specific clients instead (auth, games, sessions, etc.)
   * Direct HTTP methods for endpoints not yet migrated to modular clients
   */
  put<T>(path: string, body: unknown): Promise<T>;

  /**
   * @deprecated Use feature-specific clients instead (auth, games, sessions, etc.)
   * Direct HTTP methods for endpoints not yet migrated to modular clients
   */
  delete(path: string): Promise<void>;
}

/**
 * Create modular API client with dependency injection
 *
 * @param config Optional configuration (baseUrl, fetchImpl)
 * @returns API client with feature-specific sub-clients
 *
 * @example
 * ```typescript
 * const api = createApiClient();
 *
 * // Authentication
 * await api.auth.getProfile();
 * await api.auth.enable2FA(code);
 *
 * // Games
 * const games = await api.games.getAll({ search: 'Catan' });
 * const game = await api.games.getById(gameId);
 *
 * // Sessions
 * const session = await api.sessions.start({ gameId, players });
 * await api.sessions.complete(sessionId, { winnerName: 'Alice' });
 *
 * // Chat
 * const threads = await api.chat.getThreads(gameId);
 * const thread = await api.chat.createThread({ gameId, title });
 *
 * // PDF
 * const progress = await api.pdf.getProcessingProgress(pdfId);
 *
 * // Config
 * const configs = await api.config.getConfigurations('Features');
 *
 * // BoardGameGeek
 * const results = await api.bgg.search('Wingspan');
 * const details = await api.bgg.getGameDetails(bggId);
 * ```
 */
export function createApiClient(config?: ApiClientConfig): ApiClient {
  const httpClientConfig: Partial<HttpClientConfig> = {
    baseUrl: config?.baseUrl,
    fetchImpl: config?.fetchImpl,
  };

  const httpClient = new HttpClient(httpClientConfig);

  const client: ApiClient = {
    auth: createAuthClient({ httpClient }),
    games: createGamesClient({ httpClient }),
    sessions: createSessionsClient({ httpClient }),
    chat: createChatClient({ httpClient }),
    pdf: createPdfClient({ httpClient }),
    config: createConfigClient({ httpClient }),
    bgg: createBggClient({ httpClient }),
    agents: createAgentsClient({ httpClient }),

    // Deprecated HTTP helpers for backward compatibility
    get: <T>(path: string) => httpClient.get<T>(path),
    post: <T>(path: string, body?: unknown) => httpClient.post<T>(path, body),
    put: <T>(path: string, body: unknown) => httpClient.put<T>(path, body),
    delete: (path: string) => httpClient.delete(path),
  };

  return client;
}

/**
 * Default API client instance
 *
 * @example
 * ```typescript
 * import { api } from '@/lib/api';
 *
 * const profile = await api.auth.getProfile();
 * ```
 */
export const api: ApiClient = createApiClient();
