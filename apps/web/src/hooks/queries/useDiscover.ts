'use client';

/**
 * useDiscover — React Query hook for the /discover dashboard payload (Issue #728)
 *
 * Fetches the composite discover response (new games, top agents, recommended toolkits,
 * recent KB docs, top contributors) with a 10-minute stale time — conservative across
 * the five row TTLs to avoid stale data on the community dashboard.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { Discover } from '@/lib/api/schemas/discover.schemas';

export const discoverKeys = {
  all: ['discover'] as const,
  byLimit: (limit: number) => ['discover', { limit }] as const,
} as const;

/**
 * Hook to fetch the composite discover dashboard data.
 *
 * @param limit Items per row (1-20, default 10).
 * @param enabled Whether to run the query (default: true).
 * @returns UseQueryResult with composite Discover data or null.
 */
export function useDiscover(limit: number = 10, enabled: boolean = true) {
  return useQuery<Discover | null, Error>({
    queryKey: discoverKeys.byLimit(limit),
    queryFn: () => api.discover.getDiscover(limit),
    enabled,
    staleTime: 10 * 60_000, // 10 min — conservative across 5 row TTLs
    gcTime: 30 * 60_000,
  });
}
