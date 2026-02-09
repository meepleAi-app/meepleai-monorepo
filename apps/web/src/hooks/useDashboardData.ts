/**
 * useDashboardData Hook - Issue #3975
 *
 * React hook for fetching aggregated dashboard data with caching
 *
 * @see Epic #3901 - Dashboard Hub Core (MVP)
 * @see Backend Issue #3972 - Dashboard Aggregated API Endpoint
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { fetchDashboardData } from '@/lib/api/dashboard';
import type { DashboardData } from '@/types/dashboard';

/**
 * Fetch and cache dashboard data
 *
 * Features:
 * - 5-minute stale time (matches backend Redis cache TTL)
 * - Automatic retry on failure (2 attempts)
 * - Background refetch on window focus
 * - Error handling
 *
 * @example
 * ```tsx
 * function DashboardHub() {
 *   const { data, isLoading, error } = useDashboardData();
 *
 *   if (isLoading) return <DashboardSkeleton />;
 *   if (error) return <DashboardError error={error} />;
 *
 *   return <Dashboard data={data} />;
 * }
 * ```
 */
export function useDashboardData(): UseQueryResult<DashboardData, Error> {
  return useQuery({
    queryKey: ['dashboard'],
    queryFn: fetchDashboardData,
    staleTime: 5 * 60 * 1000, // 5 minutes (matches backend cache)
    retry: 2,
    refetchOnWindowFocus: true,
    refetchOnMount: true,
  });
}
