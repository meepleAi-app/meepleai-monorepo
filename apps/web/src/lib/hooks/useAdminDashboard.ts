/**
 * Admin Dashboard Hook (Issue #2914)
 *
 * TanStack Query hook for fetching admin dashboard analytics data
 */

import { useQuery, type UseQueryOptions } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { DashboardStats } from '@/lib/api';

export const ADMIN_DASHBOARD_QUERY_KEY = ['admin', 'analytics'] as const;

interface UseAdminDashboardOptions
  extends Omit<UseQueryOptions<DashboardStats | null, Error>, 'queryKey' | 'queryFn'> {
  /**
   * Optional parameters for analytics endpoint
   */
  params?: {
    startDate?: Date;
    endDate?: Date;
    groupBy?: string;
    roleFilter?: string;
  };
}

/**
 * Hook for fetching admin dashboard analytics data
 *
 * @param options - Query options and analytics parameters
 * @returns TanStack Query result with DashboardStats
 *
 * @example
 * ```tsx
 * const { data, isLoading, isError } = useAdminDashboard();
 * ```
 *
 * @example With polling (30s)
 * ```tsx
 * const { data } = useAdminDashboard({
 *   refetchInterval: 30000,
 *   staleTime: 25000,
 * });
 * ```
 */
export function useAdminDashboard(options?: UseAdminDashboardOptions) {
  const { params, ...queryOptions } = options || {};

  return useQuery<DashboardStats | null, Error>({
    queryKey: params ? [...ADMIN_DASHBOARD_QUERY_KEY, params] : ADMIN_DASHBOARD_QUERY_KEY,
    queryFn: async () => {
      const result = await api.admin.getAnalytics(params);
      return result;
    },
    ...queryOptions,
  });
}
