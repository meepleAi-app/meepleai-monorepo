/**
 * Games Client (FE-IMP-005)
 *
 * Modular client for GameManagement bounded context.
 * Covers: Games CRUD, filtering, sorting, pagination, documents
 */

import { z } from 'zod';

import {
  AcquireLockResultSchema,
  AgentDtoSchema,
  EditorLockSchema,
  GameFAQSchema,
  GameSchema,
  GameSessionDtoSchema,
  GameStrategyDtoSchema,
  GameReviewDtoSchema,
  GetGameFAQsResultSchema,
  GetSimilarGamesResultSchema,
  PagedStrategiesResultSchema,
  PagedReviewsResultSchema,
  PaginatedGamesResponseSchema,
  QuickQuestionSchema,
  RuleSpecDiffSchema,
  RuleSpecHistorySchema,
  RuleSpecSchema,
  VersionTimelineSchema,
  type AcquireLockResult,
  type AgentDto,
  type EditorLock,
  type Game,
  type GameFAQ,
  type GameReviewDto,
  type GameSessionDto,
  type GameStrategyDto,
  type GetGameFAQsResult,
  type GetSimilarGamesResult,
  type PagedStrategiesResult,
  type PagedReviewsResult,
  type PaginatedGamesResponse,
  type PdfDocumentDto,
  type QuickQuestion,
  type RuleSpec,
  type RuleSpecDiff,
  type RuleSpecHistory,
  type VersionTimeline,
} from '../schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateGamesClientParams {
  httpClient: HttpClient;
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

export type GameSortField =
  | 'title'
  | 'yearPublished'
  | 'minPlayers'
  | 'maxPlayers'
  | 'minPlayTimeMinutes'
  | 'maxPlayTimeMinutes';
export type SortDirection = 'asc' | 'desc';

export interface GameSortOptions {
  field: GameSortField;
  direction: SortDirection;
}

/**
 * Admin Wizard: Create game request with optional image URLs
 * Issue #2373: Added sharedGameId for catalog integration
 * Issue #3372: Added pdfId for linking PDF during game creation
 */
export interface CreateGameRequest {
  title: string;
  publisher?: string | null;
  yearPublished?: number | null;
  minPlayers?: number | null;
  maxPlayers?: number | null;
  minPlayTimeMinutes?: number | null;
  maxPlayTimeMinutes?: number | null;
  iconUrl?: string | null;
  imageUrl?: string | null;
  bggId?: number | null;
  /** Issue #2373: Link to SharedGameCatalog entry for enriched data */
  sharedGameId?: string | null;
  /** Issue #3372: Link PDF to game during creation */
  pdfId?: string | null;
}

/**
 * Games API client with Zod validation
 */
export function createGamesClient({ httpClient }: CreateGamesClientParams) {
  return {
    // ========== Games CRUD ==========

    /**
     * Get all games with optional filtering, sorting, and pagination
     * Note: Filtering, sorting, and pagination are done client-side for MVP
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
      // Fetch all games from backend with Zod validation
      // Backend returns paginated response: { games, total, page, pageSize, totalPages }
      // Backend serializes to camelCase (Program.cs:204 - JsonNamingPolicy.CamelCase)
      const response = await httpClient.get('/api/v1/games', PaginatedGamesResponseSchema);
      const allGames: Game[] = response?.games ?? [];

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
          if (
            filters.minPlayers !== undefined &&
            game.maxPlayers !== null &&
            game.maxPlayers < filters.minPlayers
          ) {
            return false;
          }
          if (
            filters.maxPlayers !== undefined &&
            game.minPlayers !== null &&
            game.minPlayers > filters.maxPlayers
          ) {
            return false;
          }

          // Play time filters
          if (
            filters.minPlayTime !== undefined &&
            game.maxPlayTimeMinutes !== null &&
            game.maxPlayTimeMinutes < filters.minPlayTime
          ) {
            return false;
          }
          if (
            filters.maxPlayTime !== undefined &&
            game.minPlayTimeMinutes !== null &&
            game.minPlayTimeMinutes > filters.maxPlayTime
          ) {
            return false;
          }

          // Year filters
          if (
            filters.yearFrom !== undefined &&
            game.yearPublished !== null &&
            game.yearPublished < filters.yearFrom
          ) {
            return false;
          }
          if (
            filters.yearTo !== undefined &&
            game.yearPublished !== null &&
            game.yearPublished > filters.yearTo
          ) {
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
        totalPages,
      };
    },

    /**
     * Get a single game by ID
     * @param id Game ID (GUID format)
     */
    async getById(id: string): Promise<Game | null> {
      return httpClient.get(`/api/v1/games/${encodeURIComponent(id)}`, GameSchema);
    },

    /**
     * Get all sessions for a specific game with optional pagination
     * Issue #1675: FE-IMP-005 - Connect frontend to backend sessions API
     * @param gameId Game ID (GUID format)
     * @param pageNumber Optional page number (1-indexed)
     * @param pageSize Optional page size (default determined by backend)
     */
    async getSessions(
      gameId: string,
      pageNumber?: number,
      pageSize?: number
    ): Promise<GameSessionDto[]> {
      const params = new URLSearchParams();
      if (pageNumber) params.append('pageNumber', pageNumber.toString());
      if (pageSize) params.append('pageSize', pageSize.toString());
      const queryString = params.toString() ? `?${params.toString()}` : '';
      const url = `/api/v1/games/${encodeURIComponent(gameId)}/sessions${queryString}`;
      const response = await httpClient.get(url, z.array(GameSessionDtoSchema));
      return response ?? [];
    },

    /**
     * Get PDF documents for a specific game
     * @param gameId Game ID (GUID format)
     */
    async getDocuments(gameId: string): Promise<PdfDocumentDto[]> {
      const response = await httpClient.get<PdfDocumentDto[]>(
        `/api/v1/games/${encodeURIComponent(gameId)}/pdfs`
      );
      return response ?? [];
    },

    /**
     * Get all RuleSpecs for a game (Issue #2027)
     * GET /api/v1/games/{gameId}/rules
     *
     * Returns all versions of RuleSpec ordered by creation date (newest first)
     */
    async getRules(gameId: string): Promise<RuleSpec[]> {
      const response = await httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/rules`,
        z.array(RuleSpecSchema)
      );
      return response ?? [];
    },

    /**
     * Create new game
     * POST /api/v1/games
     * Admin Wizard: Extended to support iconUrl and imageUrl
     */
    async create(
      request: CreateGameRequest | string
    ): Promise<{ id: string; title: string; createdAt: string }> {
      // Support both legacy (string) and new (object) signatures
      const payload = typeof request === 'string' ? { title: request } : request;
      return httpClient.post<{ id: string; title: string; createdAt: string }>(
        '/api/v1/games',
        payload
      );
    },

    /**
     * Update existing game
     * PUT /api/v1/games/{id}
     * Issue #2255: Extended to support iconUrl and imageUrl updates
     */
    async update(id: string, updates: Partial<CreateGameRequest>): Promise<Game> {
      return httpClient.put<Game>(`/api/v1/games/${encodeURIComponent(id)}`, updates, GameSchema);
    },

    /**
     * Upload game image (icon or cover)
     * POST /api/v1/games/upload-image
     * Issue #2255: File upload implementation for game creation wizard
     *
     * @param file File object to upload (PNG, JPEG, WebP, SVG)
     * @param gameId Game ID for storage organization
     * @param imageType Type of image ('icon' for thumbnails, 'image' for covers)
     * @returns Upload result with file URL and metadata
     */
    async uploadImage(
      file: File,
      gameId: string,
      imageType: 'icon' | 'image'
    ): Promise<{
      success: boolean;
      fileId?: string;
      fileUrl?: string;
      fileSizeBytes?: number;
      error?: string;
    }> {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('gameId', gameId);
      formData.append('imageType', imageType);

      // Use native fetch for FormData (httpClient doesn't support multipart)
      const baseUrl = httpClient['baseUrl'] || 'http://localhost:8080';
      const response = await fetch(`${baseUrl}/api/v1/games/upload-image`, {
        method: 'POST',
        credentials: 'include', // Send cookies for authentication
        body: formData,
        // Don't set Content-Type - browser sets it automatically with boundary for multipart
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({ message: 'Upload failed' }));
        return {
          success: false,
          error: errorData.message || `Upload failed with status ${response.status}`,
        };
      }

      const data = await response.json();
      return {
        success: data.success,
        fileId: data.fileId,
        fileUrl: data.fileUrl,
        fileSizeBytes: data.fileSizeBytes,
      };
    },

    // ========== RuleSpec Management ==========

    /**
     * Get RuleSpec for a game
     * GET /api/v1/games/{gameId}/rulespec
     *
     * Issue #1977: Added RuleSpecSchema validation
     */
    async getRuleSpec(gameId: string): Promise<RuleSpec | null> {
      return httpClient.get(`/api/v1/games/${encodeURIComponent(gameId)}/rulespec`, RuleSpecSchema);
    },

    /**
     * Get specific RuleSpec version
     * GET /api/v1/games/{gameId}/rulespec/versions/{version}
     *
     * Issue #1977: Added RuleSpecSchema validation
     */
    async getRuleSpecVersion(gameId: string, version: number): Promise<RuleSpec | null> {
      return httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/versions/${version}`,
        RuleSpecSchema
      );
    },

    /**
     * Get RuleSpec version history
     * GET /api/v1/games/{gameId}/rulespec/history
     *
     * Issue #1977: Added RuleSpecHistorySchema validation
     */
    async getRuleSpecHistory(gameId: string): Promise<RuleSpecHistory | null> {
      return httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/history`,
        RuleSpecHistorySchema
      );
    },

    /**
     * Get RuleSpec version timeline with authors
     * GET /api/v1/games/{gameId}/rulespec/versions/timeline
     *
     * Issue #1977: Added VersionTimelineSchema validation
     */
    async getRuleSpecTimeline(gameId: string): Promise<VersionTimeline | null> {
      return httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/versions/timeline`,
        VersionTimelineSchema
      );
    },

    /**
     * Update RuleSpec for a game
     * PUT /api/v1/games/{gameId}/rulespec
     *
     * Issue #1977: Added RuleSpecSchema validation
     */
    async updateRuleSpec(gameId: string, ruleSpecData: Partial<RuleSpec>): Promise<RuleSpec> {
      const result = await httpClient.put(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec`,
        ruleSpecData,
        RuleSpecSchema
      );
      if (!result) {
        throw new Error('Failed to update RuleSpec: no response from server');
      }
      return result;
    },

    /**
     * Get AI agents available for a game
     * GET /api/v1/games/{gameId}/agents
     *
     * Issue #1977: Added AgentDtoSchema validation
     */
    async getAgents(gameId: string): Promise<AgentDto[]> {
      const result = await httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/agents`,
        AgentDtoSchema.array()
      );
      return result ?? [];
    },

    /**
     * Get diff between two RuleSpec versions
     * GET /api/v1/games/{gameId}/rulespec/diff?from={from}&to={to}
     *
     * Issue #1977: Added RuleSpecDiffSchema validation
     */
    async getRuleSpecDiff(
      gameId: string,
      fromVersion: number | string,
      toVersion: number | string
    ): Promise<RuleSpecDiff | null> {
      return httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/diff?from=${encodeURIComponent(fromVersion)}&to=${encodeURIComponent(toVersion)}`,
        RuleSpecDiffSchema
      );
    },

    // ========== Game FAQs (Issue #2028) ==========

    /**
     * Get FAQs for a specific game with pagination
     * GET /api/v1/games/{gameId}/faqs?limit=10&offset=0
     * Issue #2028: Backend FAQ system for game-specific FAQs
     */
    async getFAQs(
      gameId: string,
      limit: number = 10,
      offset: number = 0
    ): Promise<GetGameFAQsResult> {
      const params = new URLSearchParams();
      params.append('limit', limit.toString());
      params.append('offset', offset.toString());
      const queryString = params.toString();
      const url = `/api/v1/games/${encodeURIComponent(gameId)}/faqs?${queryString}`;
      const result = await httpClient.get(url, GetGameFAQsResultSchema);
      return result ?? { faqs: [], totalCount: 0 };
    },

    /**
     * Upvote an FAQ
     * POST /api/v1/faqs/{id}/upvote
     * Issue #2028: Backend FAQ system for game-specific FAQs
     */
    async upvoteFAQ(faqId: string): Promise<GameFAQ> {
      const result = await httpClient.post(
        `/api/v1/faqs/${encodeURIComponent(faqId)}/upvote`,
        {},
        GameFAQSchema
      );
      if (!result) {
        throw new Error('Failed to upvote FAQ: no response from server');
      }
      return result;
    },

    // ========== Editor Locks (Issue #2055) ==========

    /**
     * Acquire editor lock for a game's RuleSpec
     * POST /api/v1/games/{gameId}/rulespec/lock
     * Issue #2055: Collaborative editing lock acquisition
     */
    async acquireEditorLock(gameId: string): Promise<AcquireLockResult> {
      const result = await httpClient.post(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/lock`,
        {},
        AcquireLockResultSchema
      );
      return result ?? { success: false, lock: null, message: 'No response from server' };
    },

    /**
     * Release editor lock for a game's RuleSpec
     * DELETE /api/v1/games/{gameId}/rulespec/lock
     * Issue #2055: Release lock when done editing
     */
    async releaseEditorLock(gameId: string): Promise<void> {
      await httpClient.delete(`/api/v1/games/${encodeURIComponent(gameId)}/rulespec/lock`);
    },

    /**
     * Get current lock status for a game's RuleSpec
     * GET /api/v1/games/{gameId}/rulespec/lock
     * Issue #2055: Check if someone else has the lock
     */
    async getEditorLockStatus(gameId: string): Promise<EditorLock | null> {
      return httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/lock`,
        EditorLockSchema
      );
    },

    /**
     * Refresh editor lock to extend TTL
     * POST /api/v1/games/{gameId}/rulespec/lock/refresh
     * Issue #2055: Keep lock alive during active editing
     */
    async refreshEditorLock(gameId: string): Promise<AcquireLockResult> {
      const result = await httpClient.post(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/lock/refresh`,
        {},
        AcquireLockResultSchema
      );
      return result ?? { success: false, lock: null, message: 'No response from server' };
    },

    /**
     * Update RuleSpec with optimistic concurrency check
     * PUT /api/v1/games/{gameId}/rulespec
     * Issue #2055: Extended to include expectedETag for conflict detection
     */
    async updateRuleSpecWithETag(
      gameId: string,
      ruleSpecData: Partial<RuleSpec>,
      expectedETag?: string
    ): Promise<RuleSpec> {
      const payload = {
        ...ruleSpecData,
        ...(expectedETag && { expectedETag }),
      };
      const result = await httpClient.put(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec`,
        payload,
        RuleSpecSchema
      );
      if (!result) {
        throw new Error('Failed to update RuleSpec: no response from server');
      }
      return result;
    },

    /**
     * Get quick questions for game
     * GET /api/v1/games/{gameId}/quick-questions
     * Issue #2401: QuickQuestion AI Generation
     *
     * Fetches AI-generated quick question suggestions for a game.
     * Falls back to empty array if questions not available.
     */
    async getQuickQuestions(gameId: string): Promise<QuickQuestion[]> {
      const response = await httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/quick-questions`,
        z.array(QuickQuestionSchema)
      );
      return response ?? [];
    },

    // ========== Similar Games (Issue #3353) ==========

    /**
     * Get similar games based on content-based filtering
     * GET /api/v1/games/{gameId}/similar
     * Issue #3353: Similar Games Discovery with RAG
     *
     * Returns games similar to the specified game based on:
     * - Categories and mechanics (content-based filtering)
     * - Player count, complexity, and duration similarity
     *
     * @param gameId Game ID to find similar games for
     * @param options Optional parameters for filtering
     * @param options.limit Maximum number of similar games (default: 10)
     * @param options.minSimilarity Minimum similarity score 0-1 (default: 0.3)
     */
    async getSimilarGames(
      gameId: string,
      options?: { limit?: number; minSimilarity?: number }
    ): Promise<GetSimilarGamesResult> {
      const params = new URLSearchParams();
      if (options?.limit) params.append('limit', options.limit.toString());
      if (options?.minSimilarity) params.append('minSimilarity', options.minSimilarity.toString());
      const queryString = params.toString() ? `?${params.toString()}` : '';
      const url = `/api/v1/games/${encodeURIComponent(gameId)}/similar${queryString}`;
      const result = await httpClient.get(url, GetSimilarGamesResultSchema);
      return result ?? { games: [], sourceGameId: gameId, sourceGameTitle: '' };
    },

    // ========== Game Strategies (Issue #4903) ==========

    /**
     * Get paginated strategies for a game
     * GET /api/v1/games/{gameId}/strategies?pageNumber=1&pageSize=10
     * Issue #4903: Game strategies API endpoint
     */
    async getStrategies(
      gameId: string,
      pageNumber: number = 1,
      pageSize: number = 10
    ): Promise<PagedStrategiesResult> {
      const params = new URLSearchParams();
      params.append('pageNumber', pageNumber.toString());
      params.append('pageSize', pageSize.toString());
      const url = `/api/v1/games/${encodeURIComponent(gameId)}/strategies?${params.toString()}`;
      const result = await httpClient.get(url, PagedStrategiesResultSchema);
      return result ?? { items: [], total: 0, page: pageNumber, pageSize };
    },

    // ========== Game Reviews (Issue #4904) ==========

    /**
     * Get paginated reviews for a game
     * GET /api/v1/games/{gameId}/reviews?pageNumber=1&pageSize=10
     * Issue #4904: Game reviews API endpoint
     */
    async getReviews(
      gameId: string,
      pageNumber: number = 1,
      pageSize: number = 10
    ): Promise<PagedReviewsResult> {
      const params = new URLSearchParams();
      params.append('pageNumber', pageNumber.toString());
      params.append('pageSize', pageSize.toString());
      const url = `/api/v1/games/${encodeURIComponent(gameId)}/reviews?${params.toString()}`;
      const result = await httpClient.get(url, PagedReviewsResultSchema);
      return result ?? { items: [], total: 0, page: pageNumber, pageSize };
    },

    /**
     * Create a review for a game
     * POST /api/v1/games/{gameId}/reviews
     * Issue #4904: Game reviews API endpoint
     */
    async createReview(
      gameId: string,
      request: { authorName: string; rating: number; content: string }
    ): Promise<GameReviewDto> {
      const result = await httpClient.post(
        `/api/v1/games/${encodeURIComponent(gameId)}/reviews`,
        request,
        GameReviewDtoSchema
      );
      if (!result) {
        throw new Error('Failed to create review: no response from server');
      }
      return result;
    },
  };
}

export type GamesClient = ReturnType<typeof createGamesClient>;

// Re-export QuickQuestion type for convenience
export type { QuickQuestion } from '../schemas';

// Re-export Similar Games types for convenience
export type { SimilarGameDto, GetSimilarGamesResult } from '../schemas';

// Re-export Strategies and Reviews types for convenience (Issue #4889)
export type { GameStrategyDto, PagedStrategiesResult, GameReviewDto, PagedReviewsResult } from '../schemas';
