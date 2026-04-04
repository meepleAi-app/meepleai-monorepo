/**
 * useUsage Hook (Game Night Improvvisata - E2-4)
 *
 * React Query hook for fetching the current user's usage snapshot.
 * Auto-refreshes every 60 seconds.
 */

'use client';

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { UsageSnapshot } from '@/lib/api/schemas/tier.schemas';

export const USAGE_QUERY_KEY = ['user', 'usage'] as const;

/**
 * Fetch current user's usage with automatic polling
 *
 * @returns React Query result with UsageSnapshot data
 */
export function useUsage() {
  return useQuery<UsageSnapshot, Error>({
    queryKey: USAGE_QUERY_KEY,
    queryFn: () => api.tiers.getMyUsage(),
    refetchInterval: 60_000, // 1 minute
    staleTime: 30_000, // 30 seconds
    retry: 2,
  });
}
