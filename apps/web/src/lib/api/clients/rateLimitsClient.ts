/**
 * Rate Limits API Client (Issue #2750)
 *
 * API client for admin rate limit configuration.
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 6 - Frontend Extensions
 */

import { type HttpClient } from '../core/httpClient';
import {
  RateLimitConfigListSchema,
  RateLimitOverrideListSchema,
  UpdateTierConfigRequestSchema,
  CreateOverrideRequestSchema,
  type RateLimitConfigDto,
  type RateLimitOverrideDto,
  type UpdateTierConfigRequest,
  type CreateOverrideRequest,
  type UserTier,
} from '../schemas/rate-limits.schemas';

export interface CreateRateLimitsClientParams {
  httpClient: HttpClient;
}

/**
 * Rate Limits API client with Zod validation
 *
 * @example
 * ```typescript
 * const client = createRateLimitsClient({ httpClient });
 *
 * // Get tier configs
 * const configs = await client.getTierConfigs();
 *
 * // Update tier config
 * await client.updateTierConfig('Premium', { maxPendingRequests: 10, maxRequestsPerMonth: 20, cooldownDays: 3 });
 *
 * // Get user overrides
 * const overrides = await client.getUserOverrides();
 *
 * // Create override
 * await client.createOverride({ userId: 'uuid', reason: 'Testing', maxPendingRequests: 100 });
 *
 * // Remove override
 * await client.removeOverride('user-uuid');
 * ```
 */
export function createRateLimitsClient({ httpClient }: CreateRateLimitsClientParams) {
  return {
    /**
     * Get all tier configurations
     *
     * @returns Array of tier configurations (Free, Basic, Premium, Pro, Admin)
     */
    async getTierConfigs(): Promise<RateLimitConfigDto[]> {
      const response = await httpClient.get('/api/v1/admin/rate-limits/configs');

      const validated = RateLimitConfigListSchema.parse(response);
      return validated.items;
    },

    /**
     * Update rate limit configuration for a specific tier
     *
     * @param tier - User tier to update (Free, Basic, Premium, Pro)
     * @param request - Updated limits
     */
    async updateTierConfig(tier: UserTier, request: UpdateTierConfigRequest): Promise<void> {
      const validated = UpdateTierConfigRequestSchema.parse(request);

      await httpClient.put(`/api/v1/admin/rate-limits/configs/${tier}`, validated);
    },

    /**
     * Get all user-specific rate limit overrides
     *
     * @param pageNumber - Page number (1-based, default: 1)
     * @param pageSize - Page size (default: 20)
     * @returns Paginated list of user overrides
     */
    async getUserOverrides(pageNumber = 1, pageSize = 20): Promise<RateLimitOverrideDto[]> {
      const response = await httpClient.get(
        `/api/v1/admin/rate-limits/overrides?pageNumber=${pageNumber}&pageSize=${pageSize}`,
      );

      const validated = RateLimitOverrideListSchema.parse(response);
      return validated.items;
    },

    /**
     * Create user-specific rate limit override
     *
     * @param request - Override configuration with reason
     */
    async createOverride(request: CreateOverrideRequest): Promise<void> {
      const validated = CreateOverrideRequestSchema.parse(request);

      await httpClient.post('/api/v1/admin/rate-limits/overrides', validated);
    },

    /**
     * Remove user-specific rate limit override
     *
     * @param userId - User UUID
     */
    async removeOverride(userId: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/rate-limits/overrides/${userId}`);
    },
  };
}

export type RateLimitsClient = ReturnType<typeof createRateLimitsClient>;
