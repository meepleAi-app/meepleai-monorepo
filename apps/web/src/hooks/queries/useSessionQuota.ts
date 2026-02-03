/**
 * useSessionQuota - TanStack Query hooks for session quota data (Issue #3075)
 *
 * Provides automatic caching and real-time quota information for game sessions.
 * Used by SessionQuotaBar and SessionQuotaSection components.
 */

import { useQuery, useQueryClient, UseQueryResult } from '@tanstack/react-query';

import { useAuth } from '@/hooks/useAuth';
import { api } from '@/lib/api';
import type { SessionQuotaResponse } from '@/lib/api/schemas/session-quota.schemas';
import {
  calculateQuotaPercentage,
  getQuotaWarningLevel,
} from '@/lib/api/schemas/session-quota.schemas';

/**
 * Query key factory for session quota queries
 */
export const sessionQuotaKeys = {
  all: ['session-quota'] as const,
  user: (userId: string) => [...sessionQuotaKeys.all, userId] as const,
};

/**
 * Hook to fetch current user's session quota
 *
 * Returns session quota information including:
 * - Current active sessions count
 * - Maximum allowed sessions for user's tier
 * - Remaining slots available
 * - Whether user can create new sessions
 *
 * Quota is cached for 1 minute and invalidated when sessions change.
 *
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with session quota data
 *
 * @example
 * ```tsx
 * const { data: quota, isLoading } = useSessionQuota();
 *
 * if (quota && !quota.canCreateNew) {
 *   // Show quota exceeded warning
 * }
 * ```
 */
export function useSessionQuota(
  enabled: boolean = true
): UseQueryResult<SessionQuotaResponse, Error> {
  const { user } = useAuth();
  const userId = user?.id;

  return useQuery({
    queryKey: userId ? sessionQuotaKeys.user(userId) : sessionQuotaKeys.all,
    queryFn: async (): Promise<SessionQuotaResponse> => {
      if (!userId) {
        throw new Error('User not authenticated');
      }
      return api.sessions.getQuota(userId);
    },
    enabled: enabled && !!userId,
    staleTime: 60 * 1000, // 1 minute - quota changes when sessions are created/ended
    gcTime: 5 * 60 * 1000, // Keep in cache for 5 minutes
    refetchOnWindowFocus: true, // Refetch when user returns to tab
  });
}

/**
 * Hook to get session quota with computed values
 *
 * Provides additional computed values:
 * - percentageUsed: Quota usage percentage (0-100)
 * - warningLevel: 'none' | 'warning' | 'critical' | 'full'
 *
 * @param enabled - Whether to run the query (default: true)
 * @returns Query result with quota and computed values
 *
 * @example
 * ```tsx
 * const { data, isLoading } = useSessionQuotaWithStatus();
 *
 * if (data) {
 *   console.log(`Using ${data.percentageUsed}% of quota`);
 *   if (data.warningLevel === 'critical') {
 *     // Show warning toast
 *   }
 * }
 * ```
 */
export function useSessionQuotaWithStatus(enabled: boolean = true): UseQueryResult<
  SessionQuotaResponse & {
    percentageUsed: number;
    warningLevel: 'none' | 'warning' | 'critical' | 'full';
  },
  Error
> {
  const { user } = useAuth();
  const userId = user?.id;

  return useQuery({
    queryKey: userId
      ? [...sessionQuotaKeys.user(userId), 'with-status']
      : [...sessionQuotaKeys.all, 'with-status'],
    queryFn: async () => {
      if (!userId) {
        throw new Error('User not authenticated');
      }
      const quota = await api.sessions.getQuota(userId);
      return {
        ...quota,
        percentageUsed: calculateQuotaPercentage(quota),
        warningLevel: getQuotaWarningLevel(quota),
      };
    },
    enabled: enabled && !!userId,
    staleTime: 60 * 1000,
    gcTime: 5 * 60 * 1000,
    refetchOnWindowFocus: true,
  });
}

/**
 * Hook to prefetch session quota data
 *
 * Useful for preloading quota data before navigation to session-related pages.
 *
 * @returns Function to prefetch quota
 *
 * @example
 * ```tsx
 * const prefetchQuota = usePrefetchSessionQuota();
 *
 * // Prefetch on hover
 * <button onMouseEnter={() => prefetchQuota()}>
 *   New Session
 * </button>
 * ```
 */
export function usePrefetchSessionQuota(): () => void {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const userId = user?.id;

  return () => {
    if (!userId) return;

    queryClient.prefetchQuery({
      queryKey: sessionQuotaKeys.user(userId),
      queryFn: () => api.sessions.getQuota(userId),
      staleTime: 60 * 1000,
    });
  };
}

/**
 * Hook to invalidate session quota cache
 *
 * Call this after creating, ending, or abandoning sessions to refresh quota.
 *
 * @returns Function to invalidate quota cache
 *
 * @example
 * ```tsx
 * const invalidateQuota = useInvalidateSessionQuota();
 *
 * const handleEndSession = async (sessionId: string) => {
 *   await api.sessions.end(sessionId);
 *   invalidateQuota();
 * };
 * ```
 */
export function useInvalidateSessionQuota(): () => void {
  const queryClient = useQueryClient();

  return () => {
    queryClient.invalidateQueries({ queryKey: sessionQuotaKeys.all });
  };
}
