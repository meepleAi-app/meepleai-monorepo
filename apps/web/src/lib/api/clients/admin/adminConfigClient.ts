/**
 * Admin Configuration Sub-Client
 *
 * Fetches system configuration by category from the admin config endpoints.
 * Used to replace hardcoded constants in admin pages with dynamic API data.
 */

import type { HttpClient } from '../../core/httpClient';

// ========== Types ==========

export interface ConfigurationDto {
  key: string;
  value: string;
}

// ========== Route Constants ==========

const CATEGORY_PATHS: Record<string, string> = {
  models: '/api/v1/admin/config/models',
  strategies: '/api/v1/admin/config/strategies',
  rerankers: '/api/v1/admin/config/rerankers',
  'rate-limits': '/api/v1/admin/config/rate-limits',
};

// ========== Client Factory ==========

export function createAdminConfigClient(http: HttpClient) {
  return {
    /**
     * Fetch configuration items by category.
     * Returns an array of ConfigurationDto objects with `key` and `value` (JSON string) fields.
     */
    async getByCategory(category: string): Promise<ConfigurationDto[]> {
      const path = CATEGORY_PATHS[category];
      if (!path) throw new Error(`Unknown config category: ${category}`);
      const result = await http.get<ConfigurationDto[]>(path);
      return result ?? [];
    },

    async getModels(): Promise<ConfigurationDto[]> {
      const result = await http.get<ConfigurationDto[]>(CATEGORY_PATHS.models);
      return result ?? [];
    },

    async getStrategies(): Promise<ConfigurationDto[]> {
      const result = await http.get<ConfigurationDto[]>(CATEGORY_PATHS.strategies);
      return result ?? [];
    },

    async getRerankers(): Promise<ConfigurationDto[]> {
      const result = await http.get<ConfigurationDto[]>(CATEGORY_PATHS.rerankers);
      return result ?? [];
    },

    async getRateLimits(): Promise<ConfigurationDto[]> {
      const result = await http.get<ConfigurationDto[]>(CATEGORY_PATHS['rate-limits']);
      return result ?? [];
    },
  };
}

export type AdminConfigClient = ReturnType<typeof createAdminConfigClient>;
