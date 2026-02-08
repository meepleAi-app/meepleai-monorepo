/**
 * useSort - Sort hook for configurable item ordering
 *
 * Manages sort state and applies sort comparators to items.
 * Supports controlled/uncontrolled patterns.
 *
 * @module components/ui/data-display/entity-list-view/hooks/use-sort
 *
 * @example
 * ```tsx
 * const sortOptions: SortOption<Game>[] = [
 *   {
 *     value: 'rating',
 *     label: 'Rating',
 *     icon: Star,
 *     compareFn: createComparator('rating', 'rating'),
 *   },
 *   {
 *     value: 'name',
 *     label: 'Name (A-Z)',
 *     compareFn: createComparator('title', 'alphabetical'),
 *   },
 * ];
 *
 * const { currentSort, setCurrentSort, sortedItems } = useSort(
 *   games,
 *   sortOptions,
 *   'rating'
 * );
 * ```
 */

import { useMemo, useState } from 'react';
import type { SortOption } from '../entity-list-view.types';

/**
 * Return type for useSort hook
 */
export interface UseSortReturn<T> {
  /** Current sort option value */
  currentSort: string;
  /** Update sort option */
  setCurrentSort: (sort: string) => void;
  /** Sorted items array */
  sortedItems: T[];
}

/**
 * Hook for managing sort functionality
 *
 * @template T - Type of items to sort
 * @param items - Array of items to sort
 * @param sortOptions - Available sort options with comparator functions
 * @param defaultSort - Default sort option value
 * @param controlledSort - Optional controlled sort value (overrides internal state)
 * @returns Object with current sort, setter, and sorted items
 */
export function useSort<T>(
  items: T[],
  sortOptions: SortOption<T>[],
  defaultSort?: string,
  controlledSort?: string
): UseSortReturn<T> {
  // Find initial sort option
  const initialSort =
    defaultSort || (sortOptions.length > 0 ? sortOptions[0].value : 'default');

  // Internal state
  const [internalSort, setInternalSort] = useState(initialSort);

  // Use controlled value if provided
  const currentSort = controlledSort ?? internalSort;

  // Apply sort to items
  const sortedItems = useMemo(() => {
    // No sort options or empty items
    if (sortOptions.length === 0 || items.length === 0) {
      return items;
    }

    // Find selected sort option
    const selectedOption = sortOptions.find((opt) => opt.value === currentSort);

    // No valid option found - return unsorted
    if (!selectedOption) {
      console.warn(`Sort option "${currentSort}" not found. Available: ${sortOptions.map((o) => o.value).join(', ')}`);
      return items;
    }

    // Create shallow copy and sort (don't mutate original)
    return [...items].sort(selectedOption.compareFn);
  }, [items, currentSort, sortOptions]);

  return {
    currentSort,
    setCurrentSort: setInternalSort,
    sortedItems,
  };
}
