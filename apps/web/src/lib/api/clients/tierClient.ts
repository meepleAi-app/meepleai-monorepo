/**
 * Tier & Usage API Client (Game Night Improvvisata - E2-4/E2-5)
 *
 * API client for user usage snapshot and tier definitions.
 * GET  /api/v1/users/me/usage      -> UsageSnapshot
 * GET  /api/v1/admin/tiers         -> TierDefinition[]
 * POST /api/v1/admin/tiers         -> TierDefinition
 * PUT  /api/v1/admin/tiers/{name}  -> TierDefinition
 */

import { type HttpClient } from '../core/httpClient';
import {
  UsageSnapshotSchema,
  TierDefinitionSchema,
  TierListResponseSchema,
  type UsageSnapshot,
  type TierDefinition,
  type TierLimit,
} from '../schemas/tier.schemas';

export interface CreateTierClientParams {
  httpClient: HttpClient;
}

export interface CreateTierRequest {
  name: string;
  displayName: string;
  description?: string | null;
  monthlyPriceEur: number;
  isActive: boolean;
  limits: TierLimit[];
}

export interface UpdateTierRequest {
  displayName: string;
  description?: string | null;
  monthlyPriceEur: number;
  isActive: boolean;
  limits: TierLimit[];
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
 *
 * // Create a new tier
 * const tier = await client.createTier({ name: 'vip', displayName: 'VIP', ... });
 *
 * // Update an existing tier
 * await client.updateTier('free', { displayName: 'Free', ... });
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

    /**
     * Create a new tier definition (admin only)
     *
     * @param request - Tier creation request
     * @returns Created tier definition
     */
    async createTier(request: CreateTierRequest): Promise<TierDefinition> {
      const response = await httpClient.post('/api/v1/admin/tiers', request);
      return TierDefinitionSchema.parse(response);
    },

    /**
     * Update an existing tier definition (admin only)
     *
     * @param name - Tier name identifier (e.g. "free", "premium")
     * @param request - Tier update request
     * @returns Updated tier definition
     */
    async updateTier(name: string, request: UpdateTierRequest): Promise<TierDefinition> {
      const response = await httpClient.put(`/api/v1/admin/tiers/${name}`, request);
      return TierDefinitionSchema.parse(response);
    },
  };
}

export type TierClient = ReturnType<typeof createTierClient>;
