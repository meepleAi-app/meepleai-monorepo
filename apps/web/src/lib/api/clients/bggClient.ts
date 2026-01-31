/**
 * BoardGameGeek Client (FE-IMP-005)
 *
 * Modular client for BoardGameGeek API integration.
 * Covers: BGG search and game details
 */

import {
  BggSearchResponseSchema,
  BggGameDetailsSchema,
  BatchThumbnailsResponseSchema,
  type BggSearchResponse,
  type BggGameDetails,
  type BatchThumbnailsResponse,
} from '../schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateBggClientParams {
  httpClient: HttpClient;
}

/**
 * BoardGameGeek API client with Zod validation
 */
export function createBggClient({ httpClient }: CreateBggClientParams) {
  return {
    // ========== BGG Search ==========

    /**
     * Search for board games on BoardGameGeek
     * @param query Search query string
     * @param exact Use exact name matching (default: false)
     * @param page Page number (default: 1)
     * @param pageSize Results per page (default: 20, max: 100)
     */
    async search(
      query: string,
      exact: boolean = false,
      page: number = 1,
      pageSize: number = 20
    ): Promise<BggSearchResponse> {
      const params = new URLSearchParams({
        q: query,
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (exact) {
        params.append('exact', 'true');
      }

      const response = await httpClient.get(
        `/api/v1/bgg/search?${params}`,
        BggSearchResponseSchema
      );

      if (!response) {
        throw new Error('Failed to search BoardGameGeek');
      }

      return response;
    },

    // ========== BGG Game Details ==========

    /**
     * Get detailed information about a board game from BoardGameGeek
     * @param bggId BoardGameGeek game ID
     */
    async getGameDetails(bggId: number): Promise<BggGameDetails> {
      const response = await httpClient.get(`/api/v1/bgg/games/${bggId}`, BggGameDetailsSchema);

      if (!response) {
        throw new Error(`Game with BGG ID ${bggId} not found`);
      }

      return response;
    },

    // ========== BGG Batch Thumbnails ==========

    /**
     * Load thumbnails for multiple games in batch
     * @param bggIds Array of BoardGameGeek game IDs (max 20)
     */
    async batchThumbnails(bggIds: number[]): Promise<BatchThumbnailsResponse> {
      if (bggIds.length === 0) {
        return {};
      }

      if (bggIds.length > 20) {
        throw new Error('Maximum 20 IDs per batch request');
      }

      const response = await httpClient.post(
        `/api/v1/bgg/thumbnails`,
        bggIds,
        BatchThumbnailsResponseSchema
      );

      if (!response) {
        throw new Error('Failed to load thumbnails');
      }

      return response;
    },
  };
}

export type BggClient = ReturnType<typeof createBggClient>;
