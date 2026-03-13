/**
 * Game Night BGG Client (E1-5)
 *
 * Client for Game Night Improvvisata BGG search and import endpoints.
 * Covers: BGG search via game-night prefix, import BGG game as PrivateGame.
 */

import { z } from 'zod';

import type { HttpClient } from '../core/httpClient';

// ============================================================================
// Schemas
// ============================================================================

const BggGameSummarySchema = z.object({
  bggId: z.number(),
  title: z.string(),
  yearPublished: z.number().nullable(),
  thumbnailUrl: z.string().nullable(),
});

const GameNightBggSearchResponseSchema = z.object({
  items: z.array(BggGameSummarySchema),
  totalCount: z.number(),
  page: z.number(),
  pageSize: z.number(),
});

const ImportBggGameResponseSchema = z.object({
  privateGameId: z.string(),
  libraryEntryId: z.string(),
  title: z.string(),
  imageUrl: z.string().nullable(),
});

// ============================================================================
// Types
// ============================================================================

export type BggGameSummary = z.infer<typeof BggGameSummarySchema>;
export type GameNightBggSearchResponse = z.infer<typeof GameNightBggSearchResponseSchema>;
export type ImportBggGameResponse = z.infer<typeof ImportBggGameResponseSchema>;

// ============================================================================
// Routes
// ============================================================================

export const GAME_NIGHT_BGG_ROUTES = {
  search: '/api/v1/game-night/bgg/search',
  import: '/api/v1/game-night/import-bgg',
} as const;

// ============================================================================
// Client
// ============================================================================

// BGG API timeout configuration (30 seconds)
const BGG_TIMEOUT_MS = 30000;

export interface CreateGameNightBggClientParams {
  httpClient: HttpClient;
}

/**
 * Game Night BGG API client with Zod validation
 */
export function createGameNightBggClient({ httpClient }: CreateGameNightBggClientParams) {
  return {
    /**
     * Search for board games via Game Night BGG search endpoint
     * @param query Search query string
     * @param page Page number (default: 1)
     * @param pageSize Results per page (default: 20)
     */
    async searchGames(
      query: string,
      page: number = 1,
      pageSize: number = 20
    ): Promise<GameNightBggSearchResponse> {
      const params = new URLSearchParams({
        q: query,
        page: page.toString(),
        pageSize: pageSize.toString(),
      });

      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), BGG_TIMEOUT_MS);

      try {
        const response = await httpClient.get(
          `${GAME_NIGHT_BGG_ROUTES.search}?${params}`,
          GameNightBggSearchResponseSchema,
          { signal: controller.signal }
        );

        if (!response) {
          throw new Error('Failed to search BoardGameGeek');
        }

        return response;
      } catch (error) {
        if (error instanceof Error && error.name === 'AbortError') {
          throw new Error(
            `BGG search timed out after ${BGG_TIMEOUT_MS / 1000}s. Please try again.`
          );
        }
        throw error;
      } finally {
        clearTimeout(timeoutId);
      }
    },

    /**
     * Import a BGG game as a PrivateGame and add to user library
     * @param bggId BoardGameGeek game ID
     */
    async importGame(bggId: number): Promise<ImportBggGameResponse> {
      const response = await httpClient.post(
        GAME_NIGHT_BGG_ROUTES.import,
        { bggId },
        ImportBggGameResponseSchema
      );

      if (!response) {
        throw new Error('Failed to import game from BoardGameGeek');
      }

      return response;
    },
  };
}

export type GameNightBggClient = ReturnType<typeof createGameNightBggClient>;
