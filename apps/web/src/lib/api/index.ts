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

import {
  createAuthClient,
  createGamesClient,
  createSessionsClient,
  createChatClient,
  createPdfClient,
  createConfigClient,
  createBggClient,
  createAgentsClient,
  createAdminClient,
  createAlertsClient,
  createDocumentsClient,
  createShareLinksClient,
  createNotificationsClient,
  createSharedGamesClient,
  createLibraryClient,
  createShareRequestsClient,
  createAdminShareRequestsClient,
  createGameContributorsClient,
  createBadgesClient,
  createRateLimitsClient,
  createEmailVerificationClient,
  createChatSessionsClient,
  createTestResultsClient,
  createTierStrategyClient,
  createDashboardClient,
  createKnowledgeBaseClient,
  createRagExecutionClient,
  createEntityLinksClient,
  createLiveSessionsClient,
  createSessionTrackingClient,
  createGameToolkitClient,
  createToolboxClient,
  createSessionStatisticsClient,
  createGameNightsClient,
  createInvitationsClient,
  createGameNightBggClient,
  createTierClient,
  createSessionInviteClient,
  createPlaylistsClient,
  createWishlistClient,
  createPlayRecordsClient,
  createFeatureFlagsClient,
  createSandboxClient,
  createOnboardingClient,
  createAccessRequestsClient,
  createAdminNotificationsClient,
  createContactClient,
  type AuthClient,
  type GamesClient,
  type SessionsClient,
  type ChatClient,
  type PdfClient,
  type ConfigClient,
  type BggClient,
  type AgentsClient,
  type AdminClient,
  type AlertsClient,
  type DocumentsClient,
  type ShareLinksClient,
  type NotificationsClient,
  type SharedGamesClient,
  type LibraryClient,
  type ShareRequestsClient,
  type AdminShareRequestsClient,
  type GameContributorsClient,
  type BadgesClient,
  type RateLimitsClient,
  type EmailVerificationClient,
  type ChatSessionsClient,
  type TestResultsClient,
  type TierStrategyClient,
  type DashboardClient,
  type KnowledgeBaseClient,
  type RagExecutionClient,
  type EntityLinksClient,
  type LiveSessionsClient,
  type SessionTrackingClient,
  type GameToolkitClient,
  type ToolboxClient,
  type SessionStatisticsClient,
  type GameNightsClient,
  type InvitationsClient,
  type GameNightBggClient,
  type TierClient,
  type SessionInviteClient,
  type PlaylistsClient,
  type WishlistClient,
  type PlayRecordsClient,
  type FeatureFlagsClient,
  type SandboxClient,
  type OnboardingClient,
  type AccessRequestsClient,
  type AdminNotificationsClient,
  type ContactClient,
} from './clients';
import { HttpClient, type HttpClientConfig } from './core/httpClient';

// Re-export alert schemas (Issue #921)
export * from './schemas/alerts.schemas';

// Re-export alert config API and schemas (Issue #915)
export * from './alert-config.api';
export * from './schemas/alert-config.schemas';

// Re-export notification schemas (Issue #2053)
export * from './schemas/notifications.schemas';

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

export type { SessionHistoryFilters } from './clients/sessionsClient';

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
export type { ProcessingProgress, ProcessingStepDto, PdfMetrics } from './schemas/pdf.schemas';
export {
  ProcessingStepSchema,
  ProcessingProgressSchema,
  PdfMetricsSchema,
} from './schemas/pdf.schemas';

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

  /** Administration (Users & Prompts Management) - Issue #1679 */
  admin: AdminClient;

  /** Alert Management (Issue #921) */
  alerts: AlertsClient;

  /** Document Collections Management (Issue #2051) */
  documents: DocumentsClient;

  /** Shareable Chat Thread Links (Issue #2052) */
  shareLinks: ShareLinksClient;

  /** User Notifications (Issue #2053) */
  notifications: NotificationsClient;

  /** Shared Game Catalog Admin (Issue #2372) */
  sharedGames: SharedGamesClient;

  /** User Game Library */
  library: LibraryClient;

  /** Share Requests (Issue #2743) */
  shareRequests: ShareRequestsClient;

  /** Admin Share Requests Review (Issue #2745) */
  adminShareRequests: AdminShareRequestsClient;

  /** Game Contributors (Issue #2746) */
  gameContributors: GameContributorsClient;

  /** Badge & Gamification System (Issue #2747) */
  badges: BadgesClient;

  /** Rate Limit Configuration (Issue #2750) */
  rateLimits: RateLimitsClient;

  /** Email Verification (Issue #3076) */
  emailVerification: EmailVerificationClient;

  /** Chat Sessions Persistence (Issue #3484) */
  chatSessions: ChatSessionsClient;

  /** Test Results History & Persistence (Issue #3379) */
  testResults: TestResultsClient;

  /** Tier-Strategy Admin Configuration (Issue #3440) */
  tierStrategy: TierStrategyClient;

  /** Dashboard Insights (Issue #3316, #3319) */
  dashboard: DashboardClient;

  /** Knowledge Base Status (Issue #4065) */
  knowledgeBase: KnowledgeBaseClient;

  /** RAG Execution Replay & Compare (Issue #4459) */
  ragExecution: RagExecutionClient;

  /** Entity Relationships (Issue #5129) */
  entityLinks: EntityLinksClient;

  /** Live Sessions — new session lifecycle (Issue #5041) */
  liveSessions: LiveSessionsClient;

  /** Session Tracking — legacy tools: dice, cards, chat, notes (Issue #5041) */
  sessionTracking: SessionTrackingClient;

  /** Game Toolkit AI Generation */
  gameToolkit: GameToolkitClient;

  /** Game Toolbox — per-game configurable containers (Epic #412) */
  toolbox: ToolboxClient;

  /** Session Analytics Dashboard (P4) */
  sessionStatistics: SessionStatisticsClient;

  /** Game Nights (Issue #33) */
  gameNights: GameNightsClient;

  /** User Invitations (Issue #132) */
  invitations: InvitationsClient;

  /** Game Night BGG Search & Import (Game Night Improvvisata) */
  gameNightBgg: GameNightBggClient;

  /** Tier & Usage (Game Night Improvvisata) */
  tiers: TierClient;

  /** Session Invites (Game Night Improvvisata) */
  sessionInvites: SessionInviteClient;

  /** Game Night Playlists — Gap Closure */
  playlists: PlaylistsClient;

  /** Wishlist */
  wishlist: WishlistClient;

  /** Play Records — session tracking and statistics (Issue #3892) */
  playRecords: PlayRecordsClient;

  /** User Feature Flags — server-driven feature access */
  featureFlags: FeatureFlagsClient;

  /** RAG Sandbox Dashboard (Admin) */
  sandbox: SandboxClient;

  /** First-time user onboarding */
  onboarding: OnboardingClient;

  /** Access Requests — invite-only registration */
  accessRequests: AccessRequestsClient;

  /** Admin manual notification dispatch */
  adminNotifications: AdminNotificationsClient;

  /** Public contact form submission */
  contact: ContactClient;

  /** Generic DELETE helper (used in some legacy tests) */
  delete: (path: string) => Promise<void>;
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
    admin: createAdminClient({ httpClient }),
    alerts: createAlertsClient({ httpClient }),
    documents: createDocumentsClient({ httpClient }),
    shareLinks: createShareLinksClient({ httpClient }), // ISSUE-2052
    notifications: createNotificationsClient({ httpClient }), // ISSUE-2053
    sharedGames: createSharedGamesClient({ httpClient }), // ISSUE-2372
    library: createLibraryClient({ httpClient }), // User library
    shareRequests: createShareRequestsClient({ httpClient }), // ISSUE-2743
    adminShareRequests: createAdminShareRequestsClient(httpClient), // ISSUE-2745
    gameContributors: createGameContributorsClient({ httpClient }), // ISSUE-2746
    badges: createBadgesClient({ httpClient }), // ISSUE-2747
    rateLimits: createRateLimitsClient({ httpClient }), // ISSUE-2750
    emailVerification: createEmailVerificationClient({ httpClient }), // ISSUE-3076
    chatSessions: createChatSessionsClient({ httpClient }), // ISSUE-3484
    testResults: createTestResultsClient({ httpClient }), // ISSUE-3379
    tierStrategy: createTierStrategyClient({ httpClient }), // ISSUE-3440
    dashboard: createDashboardClient({ httpClient }), // ISSUE-3316, ISSUE-3319
    knowledgeBase: createKnowledgeBaseClient({ httpClient }), // ISSUE-4065
    ragExecution: createRagExecutionClient({ httpClient }), // ISSUE-4459
    entityLinks: createEntityLinksClient({ httpClient }), // ISSUE-5129
    liveSessions: createLiveSessionsClient({ httpClient }), // ISSUE-5041
    sessionTracking: createSessionTrackingClient({ httpClient }), // ISSUE-5041
    gameToolkit: createGameToolkitClient({ httpClient }), // AI Toolkit Generation
    toolbox: createToolboxClient({ httpClient }), // Epic #412: Game Toolbox
    sessionStatistics: createSessionStatisticsClient({ httpClient }), // P4: Session Analytics
    gameNights: createGameNightsClient({ httpClient }), // Issue #33
    invitations: createInvitationsClient({ httpClient }), // Issue #132
    gameNightBgg: createGameNightBggClient({ httpClient }), // Game Night Improvvisata
    tiers: createTierClient({ httpClient }), // Game Night Improvvisata — Tier & Usage
    sessionInvites: createSessionInviteClient({ httpClient }), // Game Night Improvvisata — Session Invites
    playlists: createPlaylistsClient({ httpClient }), // Gap Closure — Playlists
    wishlist: createWishlistClient({ httpClient }), // Wishlist
    playRecords: createPlayRecordsClient({ httpClient }), // Play Records
    featureFlags: createFeatureFlagsClient({ httpClient }), // User Feature Flags
    sandbox: createSandboxClient({ httpClient }), // RAG Sandbox Dashboard
    onboarding: createOnboardingClient({ httpClient }), // First-time user onboarding
    accessRequests: createAccessRequestsClient({ httpClient }), // Invite-only registration
    adminNotifications: createAdminNotificationsClient({ httpClient }), // Admin manual notifications
    contact: createContactClient({ httpClient }), // Public contact form
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
