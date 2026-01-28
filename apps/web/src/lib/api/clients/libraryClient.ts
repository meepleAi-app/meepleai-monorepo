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

import {
  AgentConfigDtoSchema,
  type AgentConfigDto,
  type UpdateAgentConfigRequest,
} from '../schemas/agent-config.schemas';
import {
  PaginatedLibraryResponseSchema,
  UserLibraryStatsSchema,
  UserLibraryEntrySchema,
  GameInLibraryStatusSchema,
  LibraryQuotaResponseSchema,
  LibraryShareLinkSchema,
  SharedLibrarySchema,
  type PaginatedLibraryResponse,
  type UserLibraryStats,
  type UserLibraryEntry,
  type GameInLibraryStatus,
  type LibraryQuotaResponse,
  type GetUserLibraryParams,
  type AddGameToLibraryRequest,
  type UpdateLibraryEntryRequest,
  type LibraryShareLink,
  type CreateLibraryShareLinkRequest,
  type UpdateLibraryShareLinkRequest,
  type SharedLibrary,
} from '../schemas/library.schemas';


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
  // Agent Configuration (Issue #2518)
  getAgentConfig(gameId: string): Promise<AgentConfigDto | null>;
  updateAgentConfig(gameId: string, request: UpdateAgentConfigRequest): Promise<AgentConfigDto>;
  // Library Sharing (Issue #2614)
  getShareLink(): Promise<LibraryShareLink | null>;
  createShareLink(request: CreateLibraryShareLinkRequest): Promise<LibraryShareLink>;
  updateShareLink(shareToken: string, request: UpdateLibraryShareLinkRequest): Promise<LibraryShareLink>;
  revokeShareLink(shareToken: string): Promise<void>;
  getSharedLibrary(shareToken: string): Promise<SharedLibrary | null>;
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
          oldestAddedAt: null,
          newestAddedAt: null,
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
  };
}
