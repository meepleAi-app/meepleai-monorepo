/**
 * useActivityTimelineParams - URL state management for Activity Timeline filters
 *
 * Issue #3925 - Advanced Timeline Filters & Search
 *
 * Syncs activity filter state with URL search params for deep linking
 * and page refresh preservation. Pattern follows useCatalogSearchParams.
 *
 * URL format: /dashboard?filter=game_added,session_completed&search=catan
 *
 * @example
 * ```tsx
 * const { params, setTypes, setSearch, clearAll, hasActiveFilters } = useActivityTimelineParams();
 * ```
 */

'use client';

import { useCallback, useMemo } from 'react';

import { useSearchParams, useRouter, usePathname } from 'next/navigation';

import type { ActivityEventType } from './useActivityTimeline';

// ============================================================================
// Types
// ============================================================================

export interface ActivityTimelineParams {
  /** Selected activity types (empty = all types) */
  types: ActivityEventType[];
  /** Search query text */
  search: string;
  /** Number of items to skip (pagination offset) */
  skip: number;
  /** Number of items per page */
  take: number;
  /** Sort direction */
  order: 'asc' | 'desc';
}

export interface UseActivityTimelineParamsReturn {
  /** Current parsed params from URL */
  params: ActivityTimelineParams;
  /** Set selected filter types */
  setTypes: (types: ActivityEventType[]) => void;
  /** Set search query */
  setSearch: (search: string) => void;
  /** Toggle a single filter type */
  toggleType: (type: ActivityEventType) => void;
  /** Clear all filters and search */
  clearAll: () => void;
  /** Whether any filters are active */
  hasActiveFilters: boolean;
}

// ============================================================================
// Constants
// ============================================================================

const VALID_TYPES: ActivityEventType[] = [
  'game_added',
  'session_completed',
  'chat_saved',
  'wishlist_added',
  'achievement_unlocked',
];

const DEFAULT_PARAMS: ActivityTimelineParams = {
  types: [],
  search: '',
  skip: 0,
  take: 20,
  order: 'desc',
};

// ============================================================================
// Helpers
// ============================================================================

function parseSearchParamsToTimeline(searchParams: URLSearchParams): ActivityTimelineParams {
  const filterValue = searchParams.get('filter');
  const types = filterValue
    ? filterValue
        .split(',')
        .filter((t): t is ActivityEventType => VALID_TYPES.includes(t as ActivityEventType))
    : [];

  const search = searchParams.get('search') || '';

  return {
    types,
    search,
    skip: DEFAULT_PARAMS.skip,
    take: DEFAULT_PARAMS.take,
    order: DEFAULT_PARAMS.order,
  };
}

function buildTimelineSearchParams(params: ActivityTimelineParams): URLSearchParams {
  const urlParams = new URLSearchParams();

  if (params.types.length > 0) {
    urlParams.set('filter', params.types.join(','));
  }
  if (params.search) {
    urlParams.set('search', params.search);
  }

  return urlParams;
}

// ============================================================================
// Hook
// ============================================================================

export function useActivityTimelineParams(): UseActivityTimelineParamsReturn {
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  const params = useMemo(
    () => (searchParams ? parseSearchParamsToTimeline(searchParams) : DEFAULT_PARAMS),
    [searchParams]
  );

  const updateUrl = useCallback(
    (newParams: ActivityTimelineParams) => {
      const currentPath = pathname ?? '/library';
      const urlParams = buildTimelineSearchParams(newParams);
      const queryString = urlParams.toString();
      const newUrl = queryString ? `${currentPath}?${queryString}` : currentPath;
      router.push(newUrl, { scroll: false });
    },
    [pathname, router]
  );

  const setTypes = useCallback(
    (types: ActivityEventType[]) => {
      updateUrl({ ...params, types, skip: 0 });
    },
    [params, updateUrl]
  );

  const setSearch = useCallback(
    (search: string) => {
      updateUrl({ ...params, search, skip: 0 });
    },
    [params, updateUrl]
  );

  const toggleType = useCallback(
    (type: ActivityEventType) => {
      const newTypes = params.types.includes(type)
        ? params.types.filter(t => t !== type)
        : [...params.types, type];
      updateUrl({ ...params, types: newTypes, skip: 0 });
    },
    [params, updateUrl]
  );

  const clearAll = useCallback(() => {
    updateUrl(DEFAULT_PARAMS);
  }, [updateUrl]);

  const hasActiveFilters = useMemo(
    () => params.types.length > 0 || params.search.length > 0,
    [params.types, params.search]
  );

  return { params, setTypes, setSearch, toggleType, clearAll, hasActiveFilters };
}
