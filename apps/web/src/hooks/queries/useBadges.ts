/**
 * useBadges - TanStack Query hooks for badges & gamification
 *
 * Issue #2747: Frontend - Badge Display Components
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 5 - Gamification
 *
 * Provides automatic caching and real-time updates for badge data.
 */

import { useQuery, useMutation, UseQueryResult, UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { UserBadgeDto, LeaderboardEntryDto, LeaderboardPeriod } from '@/types/badges';
import { getQueryClient } from '@/lib/queryClient';

/**
 * Query key factory for badge queries
 */
export const badgeKeys = {
  all: ['badges'] as const,
  myBadges: () => [...badgeKeys.all, 'my-badges'] as const,
  leaderboards: () => [...badgeKeys.all, 'leaderboard'] as const,
  leaderboard: (period: LeaderboardPeriod) =>
    [...badgeKeys.leaderboards(), period] as const,
};

/**
 * Hook to fetch current user's earned badges
 *
 * Features:
 * - Automatic caching (5min stale time)
 * - Automatic refetch on window focus
 * - Loading/error states managed automatically
 *
 * @returns UseQueryResult with array of user badges
 */
export function useMyBadges(): UseQueryResult<UserBadgeDto[], Error> {
  return useQuery({
    queryKey: badgeKeys.myBadges(),
    queryFn: async (): Promise<UserBadgeDto[]> => {
      return api.badges.getMyBadges();
    },
    staleTime: 5 * 60 * 1000, // Badges don't change frequently (5min)
    refetchOnWindowFocus: true, // Refetch when user returns to tab
  });
}

/**
 * Hook to fetch leaderboard for specified period
 *
 * Features:
 * - Separate cache per period (ThisWeek, ThisMonth, AllTime)
 * - Automatic caching (2min stale time)
 * - Automatic refetch for current period
 *
 * @param period - Time period filter (default: AllTime)
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with leaderboard entries
 */
export function useLeaderboard(
  period: LeaderboardPeriod = 'AllTime',
  enabled: boolean = true
): UseQueryResult<LeaderboardEntryDto[], Error> {
  return useQuery({
    queryKey: badgeKeys.leaderboard(period),
    queryFn: async (): Promise<LeaderboardEntryDto[]> => {
      return api.badges.getLeaderboard(period);
    },
    enabled,
    staleTime: 2 * 60 * 1000, // Leaderboard changes more frequently (2min)
    refetchOnWindowFocus: true,
  });
}

/**
 * Hook to toggle badge visibility on user profile
 *
 * Features:
 * - Optimistic updates for instant UI feedback
 * - Automatic cache invalidation on success
 * - Error rollback on failure
 *
 * @returns UseMutationResult with toggle function
 *
 * @example
 * ```typescript
 * const { mutate: toggleDisplay } = useToggleBadgeDisplay();
 *
 * // Toggle badge visibility
 * toggleDisplay({ badgeId: 'uuid', isDisplayed: true });
 * ```
 */
export function useToggleBadgeDisplay(): UseMutationResult<
  void,
  Error,
  { badgeId: string; isDisplayed: boolean },
  { previousBadges: UserBadgeDto[] | undefined }
> {
  const queryClient = getQueryClient();

  return useMutation({
    mutationFn: async ({
      badgeId,
      isDisplayed,
    }: {
      badgeId: string;
      isDisplayed: boolean;
    }): Promise<void> => {
      await api.badges.toggleBadgeDisplay(badgeId, isDisplayed);
    },

    // Optimistic update: immediately update cache before API call
    onMutate: async ({ badgeId, isDisplayed }) => {
      // Cancel ongoing queries to prevent race conditions
      await queryClient.cancelQueries({ queryKey: badgeKeys.myBadges() });

      // Snapshot current cache for potential rollback
      const previousBadges = queryClient.getQueryData<UserBadgeDto[]>(
        badgeKeys.myBadges()
      );

      // Optimistically update cache
      queryClient.setQueryData<UserBadgeDto[]>(badgeKeys.myBadges(), (old) => {
        if (!old) return old; // Guard against undefined cache before first fetch
        return old.map((badge) =>
          badge.id === badgeId ? { ...badge, isDisplayed } : badge
        );
      });

      // Return context for potential rollback
      return { previousBadges };
    },

    // On error, rollback to previous state
    onError: (_err, _variables, context) => {
      if (context?.previousBadges) {
        queryClient.setQueryData(badgeKeys.myBadges(), context.previousBadges);
      }
    },

    // Always refetch after mutation (success or error)
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: badgeKeys.myBadges() });
    },
  });
}
