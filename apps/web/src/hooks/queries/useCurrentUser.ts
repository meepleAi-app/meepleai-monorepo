/**
 * useCurrentUser - TanStack Query hook for current user
 *
 * Issue #1079: FE-IMP-003 — TanStack Query Data Layer
 *
 * Replaces manual useEffect + state with automatic caching and refetching.
 * Integrates with existing AuthProvider for backward compatibility.
 */

import { useQuery, UseQueryResult } from '@tanstack/react-query';

import { getCurrentUser } from '@/actions/auth';
import { AuthUser } from '@/types';

/**
 * Query key factory for user queries
 */
export const userKeys = {
  all: ['user'] as const,
  current: () => [...userKeys.all, 'current'] as const,
};

/**
 * Hook to fetch current authenticated user
 *
 * Features:
 * - Automatic caching (5min stale time from QueryClient defaults)
 * - Automatic refetch on window focus
 * - Loading/error states managed automatically
 * - Retry logic (1 attempt from defaults)
 *
 * @returns UseQueryResult with user data, loading, and error states
 */
export function useCurrentUser(): UseQueryResult<AuthUser | null, Error> {
  return useQuery({
    queryKey: userKeys.current(),
    queryFn: async (): Promise<AuthUser | null> => {
      const result = await getCurrentUser();
      return result.user ?? null;
    },
    // Retry only on network errors, not on 401 (user not authenticated)
    retry: (failureCount, error) => {
      // Don't retry if user is simply not authenticated
      if (error.message?.includes('401') || error.message?.includes('Unauthorized')) {
        return false;
      }
      // Retry once on other errors
      return failureCount < 1;
    },
    // Refetch when user switches back to the tab (detect logout in other tab)
    refetchOnWindowFocus: true,
    // Always verify auth status on mount (don't trust cache for security-critical data)
    refetchOnMount: true,
    // Don't cache auth state (always verify with server)
    staleTime: 0,
    // Cache for at most 1 minute before considering data garbage
    gcTime: 60 * 1000,
  });
}
