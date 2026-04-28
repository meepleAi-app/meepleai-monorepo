/**
 * useTopContributors - TanStack Query hook for the top community contributors leaderboard.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596)
 *
 * Backend: GET `/api/v1/shared-games/top-contributors?limit=N` (anonymous-allowed).
 * Used by the right-sidebar leaderboard on the public Shared Games page.
 *
 * Resilience: the leaderboard is non-critical UI furniture — if the endpoint fails,
 * the sidebar should fail-silent rather than blocking the catalog grid. We therefore
 * cap retries at 1.
 */
import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api, type TopContributor } from '@/lib/api';

/**
 * Query-key factory for the top-contributors leaderboard.
 */
export const topContributorsKeys = {
  all: ['topContributors'] as const,
  byLimit: (limit: number) => [...topContributorsKeys.all, { limit }] as const,
};

/**
 * Hook to fetch the top-N community contributors.
 *
 * @param limit - Number of contributors to fetch (default 5).
 * @returns Query result with array of contributors (empty array when none/error).
 */
export function useTopContributors(limit: number = 5): UseQueryResult<TopContributor[], Error> {
  return useQuery({
    queryKey: topContributorsKeys.byLimit(limit),
    queryFn: async (): Promise<TopContributor[]> => {
      return api.sharedGames.getTopContributors(limit);
    },
    // Leaderboard updates infrequently — 5 minutes is plenty.
    staleTime: 5 * 60 * 1000,
    // Non-critical UI: don't hammer the API on a flaky response.
    retry: 1,
  });
}
