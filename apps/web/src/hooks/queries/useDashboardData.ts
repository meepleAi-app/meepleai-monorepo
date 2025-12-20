/**
 * useDashboardData - TanStack Query hooks for admin dashboard
 *
 * Issue #886: Dashboard API integration + 30s polling
 *
 * Features:
 * - 30s automatic polling interval
 * - Pauses when tab is hidden (refetchIntervalInBackground: false)
 * - Separate queries for analytics and activity (parallel loading)
 * - Automatic error retry with exponential backoff
 * - Stops polling after 3 consecutive failures
 */

import { useQuery, UseQueryResult } from '@tanstack/react-query';

import { api, type DashboardStats, type RecentActivityDto } from '@/lib/api';

/**
 * Polling configuration
 */
const POLLING_INTERVAL_MS = 30_000; // 30 seconds
const MAX_CONSECUTIVE_FAILURES = 3;

/**
 * Query key factory for admin dashboard queries
 */
export const adminKeys = {
  all: ['admin'] as const,
  dashboard: () => [...adminKeys.all, 'dashboard'] as const,
  analytics: (params?: { startDate?: Date; endDate?: Date }) =>
    [...adminKeys.dashboard(), 'analytics', params] as const,
  activity: (params?: { limit?: number; since?: Date }) =>
    [...adminKeys.dashboard(), 'activity', params] as const,
};

/**
 * Options for dashboard data hooks
 */
export interface DashboardQueryOptions {
  /** Enable 30s polling (default: true) */
  enablePolling?: boolean;
  /** Pause polling when tab is hidden (default: true) */
  pauseOnHidden?: boolean;
  /** Enable the query (default: true) */
  enabled?: boolean;
}

/**
 * Hook to fetch dashboard analytics with 30s polling
 *
 * Implements Issue #886 requirements:
 * - React Query 30s polling
 * - Pause when tab hidden
 * - Handle loading/errors
 *
 * @param options Query options
 * @returns UseQueryResult with DashboardStats
 */
export function useDashboardAnalytics(
  options: DashboardQueryOptions = {}
): UseQueryResult<DashboardStats | null, Error> {
  const { enablePolling = true, pauseOnHidden = true, enabled = true } = options;

  return useQuery({
    queryKey: adminKeys.analytics(),
    queryFn: async (): Promise<DashboardStats | null> => {
      return api.admin.getAnalytics();
    },
    enabled,
    // 30s polling interval (Issue #886 requirement)
    refetchInterval: enablePolling ? POLLING_INTERVAL_MS : false,
    // Pause polling when tab is hidden (Issue #886 requirement)
    refetchIntervalInBackground: !pauseOnHidden,
    // Refetch on window focus for immediate update when returning to tab
    refetchOnWindowFocus: true,
    // Keep data fresh for 30 seconds (matches polling interval)
    staleTime: POLLING_INTERVAL_MS,
    // Retry with exponential backoff, max 3 attempts
    retry: (failureCount, error) => {
      // Don't retry on auth errors
      if (error.message?.includes('401') || error.message?.includes('403')) {
        return false;
      }
      // Stop after 3 consecutive failures (prevent API hammering)
      return failureCount < MAX_CONSECUTIVE_FAILURES;
    },
    retryDelay: attemptIndex => Math.min(1000 * 2 ** attemptIndex, 30000),
  });
}

/**
 * Hook to fetch recent activity with 30s polling
 *
 * @param limit Number of events to fetch (default: 10)
 * @param options Query options
 * @returns UseQueryResult with RecentActivityDto
 */
export function useDashboardActivity(
  limit: number = 10,
  options: DashboardQueryOptions = {}
): UseQueryResult<RecentActivityDto, Error> {
  const { enablePolling = true, pauseOnHidden = true, enabled = true } = options;

  return useQuery({
    queryKey: adminKeys.activity({ limit }),
    queryFn: async (): Promise<RecentActivityDto> => {
      return api.admin.getRecentActivity({ limit });
    },
    enabled,
    refetchInterval: enablePolling ? POLLING_INTERVAL_MS : false,
    refetchIntervalInBackground: !pauseOnHidden,
    refetchOnWindowFocus: true,
    staleTime: POLLING_INTERVAL_MS,
    retry: (failureCount, error) => {
      if (error.message?.includes('401') || error.message?.includes('403')) {
        return false;
      }
      return failureCount < MAX_CONSECUTIVE_FAILURES;
    },
    retryDelay: attemptIndex => Math.min(1000 * 2 ** attemptIndex, 30000),
  });
}

/**
 * Combined hook for dashboard data (analytics + activity)
 *
 * Provides convenient access to both queries with unified loading/error states.
 *
 * @param activityLimit Number of activity events (default: 10)
 * @param options Query options
 * @returns Combined dashboard data with loading/error states
 */
export function useDashboardData(activityLimit: number = 10, options: DashboardQueryOptions = {}) {
  const analyticsQuery = useDashboardAnalytics(options);
  const activityQuery = useDashboardActivity(activityLimit, options);

  return {
    // Individual query results
    analytics: analyticsQuery,
    activity: activityQuery,

    // Convenience accessors
    metrics: analyticsQuery.data?.metrics ?? null,
    events: activityQuery.data?.events ?? [],

    // Combined states
    isLoading: analyticsQuery.isLoading || activityQuery.isLoading,
    isError: analyticsQuery.isError || activityQuery.isError,
    error: analyticsQuery.error || activityQuery.error,

    // Last successful update time
    lastUpdate: analyticsQuery.dataUpdatedAt ? new Date(analyticsQuery.dataUpdatedAt) : new Date(),

    // Refetch both queries
    refetch: async () => {
      await Promise.all([analyticsQuery.refetch(), activityQuery.refetch()]);
    },

    // Check if either query is currently fetching
    isFetching: analyticsQuery.isFetching || activityQuery.isFetching,
  };
}
