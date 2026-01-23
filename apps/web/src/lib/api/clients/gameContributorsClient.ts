/**
 * Game Contributors API Client (Issue #2746)
 *
 * API client for fetching contributors of shared games.
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 */

import { type HttpClient } from '../core/httpClient';
import {
  GameContributorsResponseSchema,
  type GameContributorDto,
} from '../schemas/game-contributors.schemas';

export interface CreateGameContributorsClientParams {
  httpClient: HttpClient;
}

/**
 * Game Contributors API client with Zod validation
 *
 * @example
 * ```typescript
 * const client = createGameContributorsClient({ httpClient });
 *
 * // Get contributors for a shared game
 * const contributors = await client.getGameContributors('game-uuid');
 * ```
 */
export function createGameContributorsClient({
  httpClient,
}: CreateGameContributorsClientParams) {
  return {
    /**
     * Get all contributors for a shared game
     *
     * @param gameId - Shared game UUID
     * @returns Array of contributors with their badges and stats
     */
    async getGameContributors(gameId: string): Promise<GameContributorDto[]> {
      const response = await httpClient.get(`/api/v1/shared-games/${gameId}/contributors`);

      const validated = GameContributorsResponseSchema.parse(response);
      return validated.contributors;
    },
  };
}

export type GameContributorsClient = ReturnType<typeof createGameContributorsClient>;
