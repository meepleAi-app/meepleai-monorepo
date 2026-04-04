/**
 * useFilters - Filter state management hook
 *
 * Manages filter state and applies filters to items array.
 * Supports controlled/uncontrolled patterns.
 *
 * @module components/ui/data-display/entity-list-view/hooks/use-filters
 *
 * @example
 * ```tsx
 * const filterConfig: FilterConfig<Game>[] = [
 *   {
 *     id: 'players',
 *     type: 'range',
 *     label: 'Players',
 *     field: 'maxPlayers',
 *     min: 1,
 *     max: 6,
 *   },
 *   {
 *     id: 'category',
 *     type: 'select',
 *     label: 'Category',
 *     field: 'category',
 *     options: [{ value: 'strategy', label: 'Strategy' }],
 *   },
 * ];
 *
 * const { filterState, setFilterState, filteredItems, clearFilters, activeCount } =
 *   useFilters(games, filterConfig);
 * ```
 */

import { useCallback, useMemo, useState } from 'react';

import { applyFilters, countActiveFilters } from '../utils/filter-utils';

import type { FilterConfig, FilterState, FilterValue } from '../entity-list-view.types';

/**
 * Return type for useFilters hook
 */
export interface UseFiltersReturn<T> {
  /** Current filter state */
  filterState: FilterState;
  /** Update filter state (merge or replace) */
  setFilterState: (state: FilterState | ((prev: FilterState) => FilterState)) => void;
  /** Update single filter value */
  setFilter: (filterId: string, value: FilterValue) => void;
  /** Remove single filter */
  removeFilter: (filterId: string) => void;
  /** Clear all filters */
  clearFilters: () => void;
  /** Filtered items array */
  filteredItems: T[];
  /** Count of active filters */
  activeCount: number;
}

/**
 * Hook for managing filter functionality
 *
 * @template T - Type of items to filter
 * @param items - Array of items to filter
 * @param filterConfig - Filter configuration array
 * @param controlledState - Optional controlled filter state
 * @returns Object with filter state, setters, filtered items, and active count
 */
export function useFilters<T>(
  items: T[],
  filterConfig: FilterConfig<T>[],
  controlledState?: FilterState
): UseFiltersReturn<T> {
  // Internal state
  const [internalState, setInternalState] = useState<FilterState>({});

  // Use controlled state if provided
  const filterState = controlledState ?? internalState;

  // Apply filters to items
  const filteredItems = useMemo(() => {
    if (filterConfig.length === 0) {
      return items;
    }

    return applyFilters(items, filterState, filterConfig);
  }, [items, filterState, filterConfig]);

  // Count active filters
  const activeCount = useMemo(() => {
    return countActiveFilters(filterState);
  }, [filterState]);

  // Update filter state (merge or replace)
  const setFilterState = useCallback(
    (state: FilterState | ((prev: FilterState) => FilterState)) => {
      if (typeof state === 'function') {
        setInternalState(state);
      } else {
        setInternalState(state);
      }
    },
    []
  );

  // Set single filter value
  const setFilter = useCallback((filterId: string, value: FilterValue) => {
    setInternalState(prev => ({
      ...prev,
      [filterId]: value,
    }));
  }, []);

  // Remove single filter
  const removeFilter = useCallback((filterId: string) => {
    setInternalState(prev => {
      const newState = { ...prev };
      delete newState[filterId];
      return newState;
    });
  }, []);

  // Clear all filters
  const clearFilters = useCallback(() => {
    setInternalState({});
  }, []);

  return {
    filterState,
    setFilterState,
    setFilter,
    removeFilter,
    clearFilters,
    filteredItems,
    activeCount,
  };
}
