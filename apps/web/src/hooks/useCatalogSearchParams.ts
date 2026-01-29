/**
 * useCatalogSearchParams Hook (Issue #2876)
 *
 * Manages catalog filter and pagination state with URL search params for deep linking.
 * Uses Next.js App Router's useSearchParams for bi-directional sync.
 *
 * Features:
 * - URL query param persistence for all catalog filters
 * - Deep linking support (shareable URLs)
 * - Type-safe param parsing with defaults
 * - Automatic URL updates without full page reload
 *
 * @example
 * const { params, setParams, resetParams } = useCatalogSearchParams();
 * // params.page, params.searchTerm, etc.
 * // setParams({ page: 2 }) updates URL to ?page=2
 */

'use client';

import { useCallback, useMemo } from 'react';

import { useSearchParams, useRouter, usePathname } from 'next/navigation';

export type SortByField = 'title' | 'complexity' | 'playingTime' | 'AverageRating' | 'CreatedAt';

export interface CatalogSearchParams {
  page: number;
  pageSize: number;
  searchTerm: string;
  sortBy: SortByField;
  sortDescending: boolean;
  categoryIds: string[];
  mechanicIds: string[];
  minPlayers?: number;
  maxPlayers?: number;
  maxPlayingTime?: number;
}

const DEFAULT_PARAMS: CatalogSearchParams = {
  page: 1,
  pageSize: 20,
  searchTerm: '',
  sortBy: 'title',
  sortDescending: false,
  categoryIds: [],
  mechanicIds: [],
  minPlayers: undefined,
  maxPlayers: undefined,
  maxPlayingTime: undefined,
};

/**
 * Parse URL search params to typed CatalogSearchParams
 */
function parseSearchParams(searchParams: URLSearchParams): CatalogSearchParams {
  const getNumber = (key: string, defaultValue: number): number => {
    const value = searchParams.get(key);
    if (value === null) return defaultValue;
    const parsed = parseInt(value, 10);
    return isNaN(parsed) ? defaultValue : parsed;
  };

  const getOptionalNumber = (key: string): number | undefined => {
    const value = searchParams.get(key);
    if (value === null) return undefined;
    const parsed = parseInt(value, 10);
    return isNaN(parsed) ? undefined : parsed;
  };

  const getStringArray = (key: string): string[] => {
    const value = searchParams.get(key);
    if (!value) return [];
    return value.split(',').filter(Boolean);
  };

  const getSortBy = (): SortByField => {
    const value = searchParams.get('sortBy');
    const validSortBy: SortByField[] = ['title', 'complexity', 'playingTime', 'AverageRating', 'CreatedAt'];
    return validSortBy.includes(value as SortByField) ? (value as SortByField) : 'title';
  };

  return {
    page: getNumber('page', DEFAULT_PARAMS.page),
    pageSize: getNumber('pageSize', DEFAULT_PARAMS.pageSize),
    searchTerm: searchParams.get('q') || DEFAULT_PARAMS.searchTerm,
    sortBy: getSortBy(),
    sortDescending: searchParams.get('desc') === 'true',
    categoryIds: getStringArray('categories'),
    mechanicIds: getStringArray('mechanics'),
    minPlayers: getOptionalNumber('minPlayers'),
    maxPlayers: getOptionalNumber('maxPlayers'),
    maxPlayingTime: getOptionalNumber('maxPlayingTime'),
  };
}

/**
 * Build URLSearchParams from CatalogSearchParams
 * Only includes non-default values to keep URLs clean
 */
function buildSearchParams(params: Partial<CatalogSearchParams>): URLSearchParams {
  const merged = { ...DEFAULT_PARAMS, ...params };
  const urlParams = new URLSearchParams();

  // Only add non-default values
  if (merged.page !== DEFAULT_PARAMS.page) {
    urlParams.set('page', merged.page.toString());
  }
  if (merged.pageSize !== DEFAULT_PARAMS.pageSize) {
    urlParams.set('pageSize', merged.pageSize.toString());
  }
  if (merged.searchTerm) {
    urlParams.set('q', merged.searchTerm);
  }
  if (merged.sortBy !== DEFAULT_PARAMS.sortBy) {
    urlParams.set('sortBy', merged.sortBy);
  }
  if (merged.sortDescending) {
    urlParams.set('desc', 'true');
  }
  if (merged.categoryIds.length > 0) {
    urlParams.set('categories', merged.categoryIds.join(','));
  }
  if (merged.mechanicIds.length > 0) {
    urlParams.set('mechanics', merged.mechanicIds.join(','));
  }
  if (merged.minPlayers !== undefined) {
    urlParams.set('minPlayers', merged.minPlayers.toString());
  }
  if (merged.maxPlayers !== undefined) {
    urlParams.set('maxPlayers', merged.maxPlayers.toString());
  }
  if (merged.maxPlayingTime !== undefined) {
    urlParams.set('maxPlayingTime', merged.maxPlayingTime.toString());
  }

  return urlParams;
}

export interface UseCatalogSearchParamsReturn {
  /** Current parsed params from URL */
  params: CatalogSearchParams;
  /** Update one or more params (merges with current) */
  setParams: (newParams: Partial<CatalogSearchParams>) => void;
  /** Reset all params to defaults (clears URL params) */
  resetParams: () => void;
  /** Update just the page (convenience method that resets page to 1 for filter changes) */
  setPage: (page: number) => void;
}

/**
 * Hook for managing catalog search params with URL persistence
 */
export function useCatalogSearchParams(): UseCatalogSearchParamsReturn {
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  // Parse current URL params (handle null case from useSearchParams)
  const params = useMemo(
    () => (searchParams ? parseSearchParams(searchParams) : DEFAULT_PARAMS),
    [searchParams]
  );

  // Update URL with new params (handle null pathname from usePathname)
  const setParams = useCallback(
    (newParams: Partial<CatalogSearchParams>) => {
      const currentPath = pathname ?? '/';
      const merged = { ...params, ...newParams };
      const urlParams = buildSearchParams(merged);
      const queryString = urlParams.toString();
      const newUrl = queryString ? `${currentPath}?${queryString}` : currentPath;
      router.push(newUrl, { scroll: false });
    },
    [params, pathname, router]
  );

  // Reset all params (handle null pathname from usePathname)
  const resetParams = useCallback(() => {
    router.push(pathname ?? '/', { scroll: false });
  }, [pathname, router]);

  // Convenience method for page changes
  const setPage = useCallback(
    (page: number) => {
      setParams({ page });
    },
    [setParams]
  );

  return { params, setParams, resetParams, setPage };
}
