/**
 * BoardGameGeek Client (FE-IMP-005)
 * Issue #4167: BGG API timeout handling
 *
 * Modular client for BoardGameGeek API integration.
 * Covers: BGG search and game details
 * Features: 30s timeout, automatic abort on timeout
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

// BGG API timeout configuration (30 seconds)
const BGG_TIMEOUT_MS = 30000;

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

      // Create abort controller with timeout
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), BGG_TIMEOUT_MS);

      try {
        const response = await httpClient.get(
          `/api/v1/bgg/search?${params}`,
          BggSearchResponseSchema,
          { signal: controller.signal }
        );

        if (!response) {
          throw new Error('Failed to search BoardGameGeek');
        }

        return response;
      } catch (error) {
        if (error instanceof Error && error.name === 'AbortError') {
          throw new Error(`BGG search timed out after ${BGG_TIMEOUT_MS / 1000}s. Please try again.`);
        }
        throw error;
      } finally {
        clearTimeout(timeoutId);
      }
    },

    // ========== BGG Game Details ==========

    /**
     * Get detailed information about a board game from BoardGameGeek
     * @param bggId BoardGameGeek game ID
     */
    async getGameDetails(bggId: number): Promise<BggGameDetails> {
      // Create abort controller with timeout
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), BGG_TIMEOUT_MS);

      try {
        const response = await httpClient.get(
          `/api/v1/bgg/games/${bggId}`,
          BggGameDetailsSchema,
          { signal: controller.signal }
        );

        if (!response) {
          throw new Error(`Game with BGG ID ${bggId} not found`);
        }

        return response;
      } catch (error) {
        if (error instanceof Error && error.name === 'AbortError') {
          throw new Error(`BGG game details request timed out after ${BGG_TIMEOUT_MS / 1000}s. Please try again.`);
        }
        throw error;
      } finally {
        clearTimeout(timeoutId);
      }
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
