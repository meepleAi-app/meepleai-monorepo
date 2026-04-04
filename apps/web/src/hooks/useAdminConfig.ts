/**
 * useAdminConfig Hook
 *
 * React Query hook for fetching admin configuration by category.
 * Returns ConfigurationDto[] from the backend, with 15-minute stale time.
 *
 * @example
 * ```tsx
 * const { data: configs, isLoading } = useAdminConfig('models');
 * const models = parseConfigValue<ModelType[]>(configs, 'provider_models') ?? FALLBACK;
 * ```
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { ConfigurationDto } from '@/lib/api/clients/adminClient';

export function useAdminConfig(category: string) {
  return useQuery<ConfigurationDto[]>({
    queryKey: ['admin', 'config', category],
    queryFn: () => api.admin.getByCategory(category),
    staleTime: 15 * 60 * 1000, // 15 minutes
  });
}

/**
 * Parse the JSON `value` field of a specific config key from the config array.
 * Returns undefined if the key is not found or parsing fails.
 */
export function parseConfigValue<T>(
  configs: ConfigurationDto[] | undefined,
  key: string
): T | undefined {
  if (!configs) return undefined;
  const entry = configs.find(c => c.key === key);
  if (!entry) return undefined;
  try {
    return JSON.parse(entry.value) as T;
  } catch {
    return undefined;
  }
}

/**
 * Parse all config entries in the array, returning a Record<key, parsed value>.
 */
export function parseAllConfigs<T = unknown>(
  configs: ConfigurationDto[] | undefined
): Record<string, T> {
  if (!configs) return {};
  const result: Record<string, T> = {};
  for (const entry of configs) {
    try {
      result[entry.key] = JSON.parse(entry.value) as T;
    } catch {
      // Skip entries that fail to parse
    }
  }
  return result;
}
