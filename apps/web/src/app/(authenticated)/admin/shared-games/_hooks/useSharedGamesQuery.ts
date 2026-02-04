/**
 * useSharedGamesQuery - Issue #3534
 *
 * React Query hook for fetching shared games with:
 * - URL params synchronization for shareable links
 * - Debounced search
 * - Pagination support
 * - Status and sort filters
 */

'use client';

import { useCallback, useMemo } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, useSearchParams, usePathname } from 'next/navigation';

import { api, type PagedSharedGames } from '@/lib/api';

import { type GameStatusFilter, type GameSortOption } from '../_components';

export interface UseSharedGamesQueryParams {
  /** Default page size */
  defaultPageSize?: number;
}

export interface SharedGamesFilters {
  search: string;
  status: GameStatusFilter;
  sortBy: GameSortOption;
  page: number;
  pageSize: number;
  submittedBy?: string;
}

export interface UseSharedGamesQueryResult {
  /** Games data */
  data: PagedSharedGames | undefined;
  /** Loading state */
  isLoading: boolean;
  /** Error state */
  error: Error | null;
  /** Refetch data */
  refetch: () => void;
  /** Current filters */
  filters: SharedGamesFilters;
  /** Update search (triggers debounce) */
  setSearch: (search: string) => void;
  /** Update status filter */
  setStatus: (status: GameStatusFilter) => void;
  /** Update sort option */
  setSortBy: (sortBy: GameSortOption) => void;
  /** Update page */
  setPage: (page: number) => void;
  /** Update submitter filter */
  setSubmittedBy: (submitterId: string | undefined) => void;
  /** Clear all filters */
  clearFilters: () => void;
  /** Total pages */
  totalPages: number;
}

/**
 * Convert status string to API number
 */
function statusToNumber(status: GameStatusFilter): number | undefined {
  switch (status) {
    case 'Draft':
      return 0;
    case 'PendingApproval':
      return 3;
    case 'Published':
      return 1;
    case 'Archived':
      return 2;
    default:
      return undefined;
  }
}

/**
 * Parse sort option into field and direction
 */
function parseSortOption(sortBy: GameSortOption): { field: string; desc: boolean } {
  const [field, direction] = sortBy.split(':');
  return { field, desc: direction === 'desc' };
}

export function useSharedGamesQuery(
  params: UseSharedGamesQueryParams = {}
): UseSharedGamesQueryResult {
  const { defaultPageSize = 20 } = params;

  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();

  // Read filters from URL params
  const filters: SharedGamesFilters = useMemo(() => {
    return {
      search: searchParams?.get('search') || '',
      status: (searchParams?.get('status') as GameStatusFilter) || 'all',
      sortBy: (searchParams?.get('sortBy') as GameSortOption) || 'modifiedAt:desc',
      page: parseInt(searchParams?.get('page') || '1', 10),
      pageSize: parseInt(searchParams?.get('pageSize') || String(defaultPageSize), 10),
      submittedBy: searchParams?.get('submittedBy') || undefined,
    };
  }, [searchParams, defaultPageSize]);

  // Update URL params
  const updateParams = useCallback(
    (updates: Partial<SharedGamesFilters>) => {
      const newParams = new URLSearchParams(searchParams?.toString() ?? '');

      Object.entries(updates).forEach(([key, value]) => {
        if (value === undefined || value === '' || value === 'all' || value === 'modifiedAt:desc') {
          newParams.delete(key);
        } else if (key === 'page' && value === 1) {
          newParams.delete(key);
        } else if (key === 'pageSize' && value === defaultPageSize) {
          newParams.delete(key);
        } else {
          newParams.set(key, String(value));
        }
      });

      const currentPath = pathname ?? '/admin/shared-games';
      const newUrl = newParams.toString() ? `${currentPath}?${newParams.toString()}` : currentPath;
      router.push(newUrl, { scroll: false });
    },
    [router, pathname, searchParams, defaultPageSize]
  );

  // Parse sort for API (reserved for future backend sorting support)
  const { field: _sortField, desc: _sortDescending } = parseSortOption(filters.sortBy);

  // React Query for fetching games
  const query = useQuery({
    queryKey: ['admin', 'shared-games', filters],
    queryFn: async () => {
      const statusNumber = statusToNumber(filters.status);

      // Build params for getAll which uses the admin endpoint
      const result = await api.sharedGames.getAll({
        status: statusNumber,
        page: filters.page,
        pageSize: filters.pageSize,
      });

      // Client-side filter by search (until backend supports it)
      if (filters.search.trim()) {
        const term = filters.search.toLowerCase();
        return {
          ...result,
          items: result.items.filter(
            (game) =>
              game.title.toLowerCase().includes(term) ||
              game.description.toLowerCase().includes(term)
          ),
        };
      }

      return result;
    },
    staleTime: 30000, // 30 seconds
  });

  // Calculate total pages
  const totalPages = useMemo(() => {
    if (!query.data) return 0;
    return Math.ceil(query.data.total / filters.pageSize);
  }, [query.data, filters.pageSize]);

  // Filter update handlers
  const setSearch = useCallback(
    (search: string) => {
      updateParams({ search, page: 1 }); // Reset to page 1 on search
    },
    [updateParams]
  );

  const setStatus = useCallback(
    (status: GameStatusFilter) => {
      updateParams({ status, page: 1 }); // Reset to page 1 on filter change
    },
    [updateParams]
  );

  const setSortBy = useCallback(
    (sortBy: GameSortOption) => {
      updateParams({ sortBy });
    },
    [updateParams]
  );

  const setPage = useCallback(
    (page: number) => {
      updateParams({ page });
    },
    [updateParams]
  );

  const setSubmittedBy = useCallback(
    (submitterId: string | undefined) => {
      updateParams({ submittedBy: submitterId, page: 1 });
    },
    [updateParams]
  );

  const clearFilters = useCallback(() => {
    router.push(pathname ?? '/admin/shared-games', { scroll: false });
  }, [router, pathname]);

  const refetch = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'shared-games'] });
  }, [queryClient]);

  return {
    data: query.data,
    isLoading: query.isLoading,
    error: query.error,
    refetch,
    filters,
    setSearch,
    setStatus,
    setSortBy,
    setPage,
    setSubmittedBy,
    clearFilters,
    totalPages,
  };
}
