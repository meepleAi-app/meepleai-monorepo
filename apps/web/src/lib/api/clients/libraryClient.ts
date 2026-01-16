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
  PaginatedLibraryResponseSchema,
  UserLibraryStatsSchema,
  UserLibraryEntrySchema,
  GameInLibraryStatusSchema,
  LibraryQuotaResponseSchema,
  type PaginatedLibraryResponse,
  type UserLibraryStats,
  type UserLibraryEntry,
  type GameInLibraryStatus,
  type LibraryQuotaResponse,
  type GetUserLibraryParams,
  type AddGameToLibraryRequest,
  type UpdateLibraryEntryRequest,
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
  };
}
