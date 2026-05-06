/**
 * Discover API Client (Issue #728)
 *
 * Client for the composite /discover dashboard endpoint.
 * Used by useDiscover hook for community discovery dashboard.
 */

import { discoverSchema, type Discover } from '../schemas/discover.schemas';

import type { HttpClient } from '../core/httpClient';

// ============================================================================
// Client Interface
// ============================================================================

export interface DiscoverClient {
  /**
   * Fetch the composite /discover dashboard payload.
   * @param limit Items per row (1-20, default 10).
   */
  getDiscover(limit?: number): Promise<Discover | null>;
}

// ============================================================================
// Client Factory
// ============================================================================

export interface DiscoverClientConfig {
  httpClient: HttpClient;
}

/**
 * Create Discover API client
 */
export function createDiscoverClient({ httpClient }: DiscoverClientConfig): DiscoverClient {
  return {
    async getDiscover(limit = 10) {
      const data = await httpClient.get<Discover>(`/api/v1/discover?limit=${limit}`);
      if (!data) return null;
      return discoverSchema.parse(data);
    },
  };
}
