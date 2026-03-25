/**
 * User Library API Client
 *
 * Modular client for user game library management:
 * - Get user's library with pagination and filtering
 * - Get library statistics
 * - Add/remove games from library
 * - Update library entries (notes, favorites)
 * - Check if game is in library
 */

import { z } from 'zod';

import {
  AgentConfigDtoSchema,
  type AgentConfigDto,
  type UpdateAgentConfigRequest,
} from '../schemas/agent-config.schemas';
import {
  EntityLinkDtoSchema,
  EntityLinkCountResponseSchema,
  type EntityLinkDto,
  type CreateEntityLinkRequest,
  type GetEntityLinksParams,
} from '../schemas/entity-link.schemas';
import {
  PaginatedLibraryResponseSchema,
  UserLibraryStatsSchema,
  UserLibraryEntrySchema,
  GameInLibraryStatusSchema,
  BatchGameStatusResponseSchema,
  LibraryQuotaResponseSchema,
  LibraryShareLinkSchema,
  SharedLibrarySchema,
  GameDetailDtoSchema,
  LabelDtoSchema,
  type PaginatedLibraryResponse,
  type UserLibraryStats,
  type UserLibraryEntry,
  type GameInLibraryStatus,
  type BatchGameStatusResponse,
  type LibraryQuotaResponse,
  type GetUserLibraryParams,
  type AddGameToLibraryRequest,
  type UpdateLibraryEntryRequest,
  type UpdateGameStateRequest,
  type LibraryShareLink,
  type CreateLibraryShareLinkRequest,
  type UpdateLibraryShareLinkRequest,
  type SharedLibrary,
  type GameDetailDto,
  type LabelDto,
  type CreateCustomLabelRequest,
} from '../schemas/library.schemas';
import {
  PendingMigrationDtoSchema,
  MigrationChoiceResponseSchema,
  type PendingMigrationDto,
  type MigrationChoiceRequest,
  type MigrationChoiceResponse,
} from '../schemas/migrations.schemas';
import { OwnershipResultSchema, type OwnershipResult } from '../schemas/ownership.schemas';
import { GamePdfDtoSchema, type GamePdfDto } from '../schemas/pdf.schemas';
import {
  PrivateGameDtoSchema,
  PaginatedPrivateGamesResponseSchema,
  PdfIndexingStatusSchema,
  type PrivateGameDto,
  type AddPrivateGameRequest,
  type UpdatePrivateGameRequest,
  type PdfIndexingStatus,
  type PaginatedPrivateGamesResponse,
  type GetPrivateGamesParams,
} from '../schemas/private-games.schemas';
import {
  ToolkitDashboardDtoSchema,
  type ToolkitDashboardDto,
  type OverrideToolkitRequest,
  type UpdateWidgetRequest,
} from '../schemas/toolkit.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateLibraryClientParams {
  httpClient: HttpClient;
}

export interface LibraryClient {
  getLibrary(params?: GetUserLibraryParams): Promise<PaginatedLibraryResponse>;
  getStats(): Promise<UserLibraryStats>;
  getQuota(): Promise<LibraryQuotaResponse>;
  addGame(gameId: string, request?: AddGameToLibraryRequest): Promise<UserLibraryEntry>;
  removeGame(gameId: string): Promise<void>;
  updateEntry(gameId: string, request: UpdateLibraryEntryRequest): Promise<UserLibraryEntry>;
  getGameStatus(gameId: string): Promise<GameInLibraryStatus>;
  // Batch Game Status (Issue #4581)
  getBatchGameStatus(gameIds: string[]): Promise<BatchGameStatusResponse>;
  // Game Detail (Issue #3513)
  getGameDetail(gameId: string): Promise<GameDetailDto>;
  // Game State Management (Issue #2868)
  updateGameState(gameId: string, request: UpdateGameStateRequest): Promise<void>;
  // Agent Configuration (Issue #2518)
  getAgentConfig(gameId: string): Promise<AgentConfigDto | null>;
  updateAgentConfig(gameId: string, request: UpdateAgentConfigRequest): Promise<AgentConfigDto>;
  saveAgentConfig(
    gameId: string,
    request: { typologyId: string; modelName: string; costEstimate: number }
  ): Promise<{ success: boolean; configId: string; message: string }>;
  // Library Sharing (Issue #2614)
  getShareLink(): Promise<LibraryShareLink | null>;
  createShareLink(request: CreateLibraryShareLinkRequest): Promise<LibraryShareLink>;
  updateShareLink(
    shareToken: string,
    request: UpdateLibraryShareLinkRequest
  ): Promise<LibraryShareLink>;
  revokeShareLink(shareToken: string): Promise<void>;
  getSharedLibrary(shareToken: string): Promise<SharedLibrary | null>;
  // Game Labels (Issue #3512)
  getLabels(): Promise<LabelDto[]>;
  getGameLabels(gameId: string): Promise<LabelDto[]>;
  addLabelToGame(gameId: string, labelId: string): Promise<LabelDto>;
  removeLabelFromGame(gameId: string, labelId: string): Promise<void>;
  createCustomLabel(request: CreateCustomLabelRequest): Promise<LabelDto>;
  deleteCustomLabel(labelId: string): Promise<void>;
  // Private Games (Issue #3669)
  getPrivateGames(params?: GetPrivateGamesParams): Promise<PaginatedPrivateGamesResponse>;
  getPrivateGame(id: string): Promise<PrivateGameDto>;
  addPrivateGame(request: AddPrivateGameRequest): Promise<PrivateGameDto>;
  updatePrivateGame(id: string, request: UpdatePrivateGameRequest): Promise<PrivateGameDto>;
  deletePrivateGame(id: string): Promise<void>;
  // PDF Indexing Status (Issue #4946)
  getPdfProcessingStatus(gameId: string): Promise<PdfIndexingStatus>;
  // Proposal Migrations (Issue #3669)
  getPendingMigrations(): Promise<PendingMigrationDto[]>;
  chooseMigration(
    migrationId: string,
    request: MigrationChoiceRequest
  ): Promise<MigrationChoiceResponse>;
  // Game PDFs (Issue #4915)
  getGamePdfs(gameId: string): Promise<GamePdfDto[]>;
  // EntityLinks (Issue #5142 — Epic A)
  getEntityLinks(params: GetEntityLinksParams): Promise<EntityLinkDto[]>;
  getEntityLinkCount(entityType: string, entityId: string): Promise<number>;
  createEntityLink(request: CreateEntityLinkRequest): Promise<EntityLinkDto>;
  deleteEntityLink(linkId: string): Promise<void>;
  // Ownership Declaration (RAG Access)
  declareOwnership(gameId: string): Promise<OwnershipResult>;
  // Toolkit Dashboard (Issue #5147 — Epic B4)
  getActiveToolkit(gameId: string): Promise<ToolkitDashboardDto | null>;
  overrideToolkit(gameId: string, request?: OverrideToolkitRequest): Promise<ToolkitDashboardDto>;
  updateToolkitWidget(
    gameId: string,
    widgetType: string,
    request: UpdateWidgetRequest
  ): Promise<ToolkitDashboardDto>;
}

/**
 * Create library client with HttpClient dependency injection
 */
export function createLibraryClient({ httpClient }: CreateLibraryClientParams): LibraryClient {
  return {
    /**
     * Get user's game library with pagination and filtering
     * @param params - Optional filtering and pagination parameters
     */
    async getLibrary(params?: GetUserLibraryParams): Promise<PaginatedLibraryResponse> {
      const queryParams = new URLSearchParams();

      if (params?.page !== undefined) {
        queryParams.append('page', String(params.page));
      }
      if (params?.pageSize !== undefined) {
        queryParams.append('pageSize', String(params.pageSize));
      }
      if (params?.search) {
        queryParams.append('search', params.search);
      }
      if (params?.favoritesOnly !== undefined) {
        queryParams.append('favoritesOnly', String(params.favoritesOnly));
      }
      // State filter support (Issue #2866)
      if (params?.stateFilter && params.stateFilter.length > 0) {
        for (const state of params.stateFilter) {
          queryParams.append('stateFilter', state);
        }
      }
      if (params?.sortBy !== undefined) {
        queryParams.append('sortBy', params.sortBy);
      }
      if (params?.sortDescending !== undefined) {
        queryParams.append('sortDescending', String(params.sortDescending));
      }

      const queryString = queryParams.toString();
      const url = queryString ? `/api/v1/library?${queryString}` : '/api/v1/library';

      const data = await httpClient.get<PaginatedLibraryResponse>(
        url,
        PaginatedLibraryResponseSchema
      );
      return (
        data ?? {
          items: [],
          page: 1,
          pageSize: 20,
          totalCount: 0,
          totalPages: 0,
          hasNextPage: false,
          hasPreviousPage: false,
        }
      );
    },

    /**
     * Get library statistics for authenticated user
     */
    async getStats(): Promise<UserLibraryStats> {
      const data = await httpClient.get<UserLibraryStats>(
        '/api/v1/library/stats',
        UserLibraryStatsSchema
      );
      return (
        data ?? {
          totalGames: 0,
          favoriteGames: 0,
          privatePdfs: 0,
          oldestAddedAt: null,
          newestAddedAt: null,
          nuovoCount: 0,
          inPrestitoCount: 0,
          wishlistCount: 0,
          ownedCount: 0,
        }
      );
    },

    /**
     * Get library quota information for authenticated user (Issue #2445)
     * Returns current usage, limits, and tier information
     */
    async getQuota(): Promise<LibraryQuotaResponse> {
      const data = await httpClient.get<LibraryQuotaResponse>(
        '/api/v1/library/quota',
        LibraryQuotaResponseSchema
      );
      return (
        data ?? {
          currentCount: 0,
          maxAllowed: 5,
          userTier: 'free',
          remainingSlots: 5,
          percentageUsed: 0,
        }
      );
    },

    /**
     * Add a game to user's library
     * @param gameId - Game UUID to add
     * @param request - Optional notes and favorite status
     */
    async addGame(gameId: string, request?: AddGameToLibraryRequest): Promise<UserLibraryEntry> {
      const data = await httpClient.post<UserLibraryEntry>(
        `/api/v1/library/games/${gameId}`,
        request ?? {},
        UserLibraryEntrySchema
      );
      if (!data) {
        throw new Error('Failed to add game to library');
      }
      return data;
    },

    /**
     * Remove a game from user's library
     * @param gameId - Game UUID to remove
     */
    async removeGame(gameId: string): Promise<void> {
      await httpClient.delete(`/api/v1/library/games/${gameId}`);
    },

    /**
     * Update a library entry (notes, favorite status)
     * @param gameId - Game UUID to update
     * @param request - Update data (notes, isFavorite)
     */
    async updateEntry(
      gameId: string,
      request: UpdateLibraryEntryRequest
    ): Promise<UserLibraryEntry> {
      const data = await httpClient.patch<UserLibraryEntry>(
        `/api/v1/library/games/${gameId}`,
        request,
        UserLibraryEntrySchema
      );
      if (!data) {
        throw new Error('Failed to update library entry');
      }
      return data;
    },

    /**
     * Check if a game is in user's library
     * @param gameId - Game UUID to check
     */
    async getGameStatus(gameId: string): Promise<GameInLibraryStatus> {
      const data = await httpClient.get<GameInLibraryStatus>(
        `/api/v1/library/games/${gameId}/status`,
        GameInLibraryStatusSchema
      );
      return data ?? { inLibrary: false, isFavorite: false };
    },

    /**
     * Batch check library status for multiple games (Issue #4581)
     * Eliminates N+1 problem when rendering game grids
     * @param gameIds - Array of game UUIDs to check (max 100)
     * @returns Dictionary of game statuses indexed by gameId
     */
    async getBatchGameStatus(gameIds: string[]): Promise<BatchGameStatusResponse> {
      if (gameIds.length === 0) {
        return { results: {}, totalChecked: 0 };
      }
      const idsParam = gameIds.join(',');
      const data = await httpClient.get<BatchGameStatusResponse>(
        `/api/v1/library/games/batch-status?gameIds=${idsParam}`,
        BatchGameStatusResponseSchema
      );
      return data ?? { results: {}, totalChecked: 0 };
    },

    /**
     * Get comprehensive game detail with stats, sessions, and checklist (Issue #3513)
     * @param gameId - Game UUID in user's library
     * @returns Complete game detail with all metadata and statistics
     */
    async getGameDetail(gameId: string): Promise<GameDetailDto> {
      if (
        !gameId ||
        !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(gameId)
      ) {
        throw new Error('Invalid game ID');
      }
      const data = await httpClient.get<GameDetailDto>(
        `/api/v1/library/games/${gameId}`,
        GameDetailDtoSchema
      );
      if (!data) {
        throw new Error('Game not found in library');
      }
      return data;
    },

    /**
     * Update game state (Nuovo/InPrestito/Wishlist/Owned) (Issue #2868)
     * @param gameId - Game UUID to update
     * @param request - New state and optional notes
     */
    async updateGameState(gameId: string, request: UpdateGameStateRequest): Promise<void> {
      await httpClient.put(`/api/v1/library/games/${gameId}/state`, request);
    },

    /**
     * Get agent configuration for a game in user's library (Issue #2518)
     * @param gameId - Game UUID
     * @returns Agent configuration or null if not configured
     */
    async getAgentConfig(gameId: string): Promise<AgentConfigDto | null> {
      return httpClient.get<AgentConfigDto>(
        `/api/v1/library/games/${gameId}/agent-config`,
        AgentConfigDtoSchema
      );
    },

    /**
     * Update agent configuration for a game in user's library (Issue #2518)
     * @param gameId - Game UUID
     * @param request - Agent configuration update data
     * @returns Updated agent configuration
     */
    async updateAgentConfig(
      gameId: string,
      request: UpdateAgentConfigRequest
    ): Promise<AgentConfigDto> {
      const data = await httpClient.put<AgentConfigDto>(
        `/api/v1/library/games/${gameId}/agent-config`,
        request,
        AgentConfigDtoSchema
      );
      if (!data) {
        throw new Error('Failed to update agent configuration');
      }
      return data;
    },

    /**
     * Save simplified agent configuration from modal (Issue #3212)
     * @param gameId - Game UUID
     * @param request - Simplified agent config (typology + model + cost)
     * @returns Save response with config ID
     */
    async saveAgentConfig(
      gameId: string,
      request: { typologyId: string; modelName: string; costEstimate: number }
    ): Promise<{ success: boolean; configId: string; message: string }> {
      const data = await httpClient.post<{
        success: boolean;
        configId: string;
        message: string;
      }>(
        `/api/v1/library/games/${gameId}/agent-config`,
        request,
        z.object({
          success: z.boolean(),
          configId: z.string().uuid(),
          message: z.string(),
        })
      );
      if (!data) {
        throw new Error('Failed to save agent configuration');
      }
      return data;
    },

    // ========================================
    // Library Sharing Methods (Issue #2614)
    // ========================================

    /**
     * Get the user's active share link
     * @returns Active share link or null if none exists
     */
    async getShareLink(): Promise<LibraryShareLink | null> {
      return httpClient.get<LibraryShareLink>('/api/v1/library/share', LibraryShareLinkSchema);
    },

    /**
     * Create a new share link for the user's library
     * This revokes any existing active share link
     * @param request - Share link configuration
     * @returns Created share link
     */
    async createShareLink(request: CreateLibraryShareLinkRequest): Promise<LibraryShareLink> {
      const data = await httpClient.post<LibraryShareLink>(
        '/api/v1/library/share',
        request,
        LibraryShareLinkSchema
      );
      if (!data) {
        throw new Error('Failed to create share link');
      }
      return data;
    },

    /**
     * Update an existing share link's settings
     * @param shareToken - Share token to update
     * @param request - Updated settings
     * @returns Updated share link
     */
    async updateShareLink(
      shareToken: string,
      request: UpdateLibraryShareLinkRequest
    ): Promise<LibraryShareLink> {
      const data = await httpClient.patch<LibraryShareLink>(
        `/api/v1/library/share/${shareToken}`,
        request,
        LibraryShareLinkSchema
      );
      if (!data) {
        throw new Error('Failed to update share link');
      }
      return data;
    },

    /**
     * Revoke a share link (permanently disable it)
     * @param shareToken - Share token to revoke
     */
    async revokeShareLink(shareToken: string): Promise<void> {
      await httpClient.delete(`/api/v1/library/share/${shareToken}`);
    },

    /**
     * Get a shared library by its share token (public access, no auth required)
     * @param shareToken - Share token from URL
     * @returns Shared library data or null if not found/invalid
     */
    async getSharedLibrary(shareToken: string): Promise<SharedLibrary | null> {
      return httpClient.get<SharedLibrary>(
        `/api/v1/library/shared/${shareToken}`,
        SharedLibrarySchema
      );
    },

    // ========================================
    // Game Labels Methods (Issue #3512)
    // ========================================

    /**
     * Get all available labels (predefined + user custom)
     * @returns Array of label DTOs
     */
    async getLabels(): Promise<LabelDto[]> {
      const data = await httpClient.get<LabelDto[]>(
        '/api/v1/library/labels',
        z.array(LabelDtoSchema)
      );
      return data ?? [];
    },

    /**
     * Get labels assigned to a specific game
     * @param gameId - Game UUID in user's library
     * @returns Array of labels assigned to the game
     */
    async getGameLabels(gameId: string): Promise<LabelDto[]> {
      const data = await httpClient.get<LabelDto[]>(
        `/api/v1/library/games/${gameId}/labels`,
        z.array(LabelDtoSchema)
      );
      return data ?? [];
    },

    /**
     * Add a label to a game in user's library
     * @param gameId - Game UUID
     * @param labelId - Label UUID (predefined or custom)
     * @returns The added label DTO
     */
    async addLabelToGame(gameId: string, labelId: string): Promise<LabelDto> {
      const data = await httpClient.post<LabelDto>(
        `/api/v1/library/games/${gameId}/labels/${labelId}`,
        {},
        LabelDtoSchema
      );
      if (!data) {
        throw new Error('Failed to add label to game');
      }
      return data;
    },

    /**
     * Remove a label from a game in user's library
     * @param gameId - Game UUID
     * @param labelId - Label UUID to remove
     */
    async removeLabelFromGame(gameId: string, labelId: string): Promise<void> {
      await httpClient.delete(`/api/v1/library/games/${gameId}/labels/${labelId}`);
    },

    /**
     * Create a custom label for the user
     * @param request - Label name and color
     * @returns Created label DTO
     */
    async createCustomLabel(request: CreateCustomLabelRequest): Promise<LabelDto> {
      const data = await httpClient.post<LabelDto>(
        '/api/v1/library/labels',
        request,
        LabelDtoSchema
      );
      if (!data) {
        throw new Error('Failed to create custom label');
      }
      return data;
    },

    /**
     * Delete a custom label
     * @param labelId - Label UUID (must be user's custom label, not predefined)
     */
    async deleteCustomLabel(labelId: string): Promise<void> {
      await httpClient.delete(`/api/v1/library/labels/${labelId}`);
    },

    // ==================== Private Games (Issue #3669) ====================

    /**
     * Get user's private games with pagination
     * @param params - Optional filtering and pagination
     * @returns Paginated list of private games
     */
    async getPrivateGames(params?: GetPrivateGamesParams): Promise<PaginatedPrivateGamesResponse> {
      const queryParams = new URLSearchParams();

      if (params?.page !== undefined) {
        queryParams.append('page', String(params.page));
      }
      if (params?.pageSize !== undefined) {
        queryParams.append('pageSize', String(params.pageSize));
      }
      if (params?.search) {
        queryParams.append('search', params.search);
      }
      if (params?.sortBy) {
        queryParams.append('sortBy', params.sortBy);
      }
      if (params?.sortDirection) {
        queryParams.append('sortDirection', params.sortDirection);
      }

      const url = `/api/v1/private-games?${queryParams.toString()}`;
      const data = await httpClient.get<PaginatedPrivateGamesResponse>(
        url,
        PaginatedPrivateGamesResponseSchema
      );

      if (!data) {
        throw new Error('Failed to fetch private games');
      }

      return data;
    },

    /**
     * Get a single private game by ID
     * @param id - Private game UUID
     * @returns Private game details
     */
    async getPrivateGame(id: string): Promise<PrivateGameDto> {
      const data = await httpClient.get<PrivateGameDto>(
        `/api/v1/private-games/${id}`,
        PrivateGameDtoSchema
      );

      if (!data) {
        throw new Error('Failed to fetch private game');
      }

      return data;
    },

    /**
     * Add a new private game (manual or from BGG)
     * @param request - Private game data
     * @returns Created private game
     */
    async addPrivateGame(request: AddPrivateGameRequest): Promise<PrivateGameDto> {
      const data = await httpClient.post<PrivateGameDto>(
        '/api/v1/private-games',
        request,
        PrivateGameDtoSchema
      );

      if (!data) {
        throw new Error('Failed to add private game');
      }

      return data;
    },

    /**
     * Update an existing private game
     * @param id - Private game UUID
     * @param request - Updated game data
     * @returns Updated private game
     */
    async updatePrivateGame(
      id: string,
      request: UpdatePrivateGameRequest
    ): Promise<PrivateGameDto> {
      const data = await httpClient.put<PrivateGameDto>(
        `/api/v1/private-games/${id}`,
        request,
        PrivateGameDtoSchema
      );

      if (!data) {
        throw new Error('Failed to update private game');
      }

      return data;
    },

    /**
     * Delete a private game (soft delete)
     * @param id - Private game UUID
     */
    async deletePrivateGame(id: string): Promise<void> {
      await httpClient.delete(`/api/v1/private-games/${id}`);
    },

    // ==================== PDF Indexing Status (Issue #4946) ====================

    /**
     * Get PDF indexing/processing status for a game
     * @param gameId - Game UUID
     * @returns PDF indexing status with progress, chunkCount and errorMessage
     */
    async getPdfProcessingStatus(gameId: string): Promise<PdfIndexingStatus> {
      const data = await httpClient.get<PdfIndexingStatus>(
        `/api/v1/library/games/${gameId}/pdf-status`,
        PdfIndexingStatusSchema
      );

      if (!data) {
        throw new Error('Failed to fetch PDF processing status');
      }

      return data;
    },

    // ==================== Proposal Migrations (Issue #3669) ====================

    /**
     * Get pending migration choices for approved proposals
     * @returns List of pending migrations requiring user decision
     */
    async getPendingMigrations(): Promise<PendingMigrationDto[]> {
      const data = await httpClient.get<PendingMigrationDto[]>(
        '/api/v1/migrations/pending',
        z.array(PendingMigrationDtoSchema)
      );

      if (!data) {
        throw new Error('Failed to fetch pending migrations');
      }

      return data;
    },

    /**
     * Handle migration choice (keep private vs migrate to shared)
     * @param migrationId - Migration UUID
     * @param request - Migration action choice
     * @returns Migration choice result
     */
    async chooseMigration(
      migrationId: string,
      request: MigrationChoiceRequest
    ): Promise<MigrationChoiceResponse> {
      const data = await httpClient.post<MigrationChoiceResponse>(
        `/api/v1/migrations/${migrationId}/choose`,
        request,
        MigrationChoiceResponseSchema
      );

      if (!data) {
        throw new Error('Failed to process migration choice');
      }

      return data;
    },

    /**
     * Get PDFs for a game in user's library (custom + catalog)
     * Issue #4915: Agent creation wizard PDF selection
     * @param gameId - Game UUID
     */
    async getGamePdfs(gameId: string): Promise<GamePdfDto[]> {
      const data = await httpClient.get<GamePdfDto[]>(
        `/api/v1/library/games/${gameId}/pdfs`,
        z.array(GamePdfDtoSchema)
      );
      return data ?? [];
    },

    // ========== EntityLink User Methods (Issue #5142 — Epic A) ==========

    /**
     * Get entity links for an entity (user scope — own + approved shared)
     * GET /api/v1/library/entity-links?entityType=...&entityId=...&linkType=...
     */
    async getEntityLinks(params: GetEntityLinksParams): Promise<EntityLinkDto[]> {
      const qs = new URLSearchParams();
      qs.set('entityType', params.entityType);
      qs.set('entityId', params.entityId);
      if (params.linkType) qs.set('linkType', params.linkType);
      const data = await httpClient.get<EntityLinkDto[]>(
        `/api/v1/library/entity-links?${qs.toString()}`,
        z.array(EntityLinkDtoSchema)
      );
      return data ?? [];
    },

    /**
     * Get count of entity links for an entity
     * GET /api/v1/library/entity-links/count?entityType=...&entityId=...
     */
    async getEntityLinkCount(entityType: string, entityId: string): Promise<number> {
      const qs = new URLSearchParams({ entityType, entityId });
      const data = await httpClient.get(
        `/api/v1/library/entity-links/count?${qs.toString()}`,
        EntityLinkCountResponseSchema
      );
      return data?.count ?? 0;
    },

    /**
     * Create a user-scoped entity link
     * POST /api/v1/library/entity-links
     */
    async createEntityLink(request: CreateEntityLinkRequest): Promise<EntityLinkDto> {
      const result = await httpClient.post<EntityLinkDto>(
        '/api/v1/library/entity-links',
        request,
        EntityLinkDtoSchema
      );
      if (!result) throw new Error('Failed to create entity link');
      return result;
    },

    /**
     * Delete a user-scoped entity link (owner only)
     * DELETE /api/v1/library/entity-links/{linkId}
     */
    async deleteEntityLink(linkId: string): Promise<void> {
      await httpClient.delete(`/api/v1/library/entity-links/${linkId}`);
    },

    // ========== Ownership Declaration (RAG Access) ==========

    /**
     * Declare ownership of a game in user's library
     * Transitions game state and grants RAG access if available
     * POST /api/v1/library/{gameId}/declare-ownership
     * @param gameId - Game UUID to declare ownership of
     * @returns Ownership result with RAG access status
     */
    async declareOwnership(gameId: string): Promise<OwnershipResult> {
      const data = await httpClient.post<OwnershipResult>(
        `/api/v1/library/${gameId}/declare-ownership`,
        {},
        OwnershipResultSchema
      );
      if (!data) {
        throw new Error('Failed to declare ownership');
      }
      return data;
    },

    // ========== Toolkit Dashboard (Issue #5147 — Epic B4) ==========

    /**
     * Get the active toolkit for a game (user override or shared default).
     * Returns null when no toolkit exists yet (204 from API).
     * GET /api/v1/library/games/{gameId}/toolkit
     */
    async getActiveToolkit(gameId: string): Promise<ToolkitDashboardDto | null> {
      return httpClient.get<ToolkitDashboardDto>(
        `/api/v1/library/games/${gameId}/toolkit`,
        ToolkitDashboardDtoSchema
      );
    },

    /**
     * Create or update the user's toolkit override for a game.
     * PUT /api/v1/library/games/{gameId}/toolkit
     */
    async overrideToolkit(
      gameId: string,
      request: OverrideToolkitRequest = {}
    ): Promise<ToolkitDashboardDto> {
      const data = await httpClient.put<ToolkitDashboardDto>(
        `/api/v1/library/games/${gameId}/toolkit`,
        request,
        ToolkitDashboardDtoSchema
      );
      if (!data) throw new Error('Failed to create toolkit override');
      return data;
    },

    /**
     * Enable/disable or reconfigure a single widget in the active toolkit.
     * PATCH /api/v1/library/games/{gameId}/toolkit/widgets/{widgetType}
     */
    async updateToolkitWidget(
      gameId: string,
      widgetType: string,
      request: UpdateWidgetRequest
    ): Promise<ToolkitDashboardDto> {
      const data = await httpClient.patch<ToolkitDashboardDto>(
        `/api/v1/library/games/${gameId}/toolkit/widgets/${widgetType}`,
        request,
        ToolkitDashboardDtoSchema
      );
      if (!data) throw new Error('Failed to update toolkit widget');
      return data;
    },
  };
}
