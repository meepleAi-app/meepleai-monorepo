/**
 * BoardGameGeek Client (FE-IMP-005)
 *
 * Modular client for BoardGameGeek API integration.
 * Covers: BGG search and game details
 */

import type { HttpClient } from '../core/httpClient';
import {
  BggSearchResponseSchema,
  BggGameDetailsSchema,
  type BggSearchResponse,
  type BggGameDetails,
} from '../schemas';

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
     */
    async search(query: string, exact: boolean = false): Promise<BggSearchResponse> {
      const params = new URLSearchParams({ q: query });
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
      const response = await httpClient.get(
        `/api/v1/bgg/games/${bggId}`,
        BggGameDetailsSchema
      );

      if (!response) {
        throw new Error(`Game with BGG ID ${bggId} not found`);
      }

      return response;
    },
  };
}

export type BggClient = ReturnType<typeof createBggClient>;
