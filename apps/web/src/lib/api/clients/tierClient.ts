/**
 * Tier & Usage API Client (Game Night Improvvisata - E2-4/E2-5)
 *
 * API client for user usage snapshot and tier definitions.
 * GET /api/v1/users/me/usage -> UsageSnapshot
 * GET /api/v1/admin/tiers -> TierDefinition[]
 */

import { type HttpClient } from '../core/httpClient';
import {
  UsageSnapshotSchema,
  TierListResponseSchema,
  type UsageSnapshot,
  type TierDefinition,
} from '../schemas/tier.schemas';

export interface CreateTierClientParams {
  httpClient: HttpClient;
}

/**
 * Tier & Usage API client with Zod validation
 *
 * @example
 * ```typescript
 * const client = createTierClient({ httpClient });
 *
 * // Get current user's usage
 * const usage = await client.getMyUsage();
 *
 * // Get all tier definitions
 * const tiers = await client.getTiers();
 * ```
 */
export function createTierClient({ httpClient }: CreateTierClientParams) {
  return {
    /**
     * Get current user's usage snapshot
     *
     * @returns Usage snapshot with current/max values for all limits
     */
    async getMyUsage(): Promise<UsageSnapshot> {
      const response = await httpClient.get('/api/v1/users/me/usage');
      return UsageSnapshotSchema.parse(response);
    },

    /**
     * Get all tier definitions (admin endpoint, publicly readable)
     *
     * @returns Array of tier definitions with limits
     */
    async getTiers(): Promise<TierDefinition[]> {
      const response = await httpClient.get('/api/v1/admin/tiers');
      return TierListResponseSchema.parse(response);
    },
  };
}

export type TierClient = ReturnType<typeof createTierClient>;
