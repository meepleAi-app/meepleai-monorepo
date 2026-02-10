/**
 * useSearch - Search hook with debounce and fuzzy matching
 *
 * Manages search query state and filters items based on specified fields.
 * Features 300ms debounce to avoid expensive re-filtering on every keystroke.
 *
 * @module components/ui/data-display/entity-list-view/hooks/use-search
 *
 * @example
 * ```tsx
 * const { query, setQuery, filteredItems } = useSearch(
 *   games,
 *   ['title', 'publisher', 'description']
 * );
 *
 * <SearchBar value={query} onChange={setQuery} />
 * <GameList items={filteredItems} />
 * ```
 */

import { useMemo, useState } from 'react';

import { useDebounce } from './use-debounce';
import { fuzzySearch } from '../utils/search-utils';

/**
 * Return type for useSearch hook
 */
export interface UseSearchReturn<T> {
  /** Current search query */
  query: string;
  /** Update search query */
  setQuery: (query: string) => void;
  /** Filtered items matching the query */
  filteredItems: T[];
}

/**
 * Hook for managing search functionality with debounce
 *
 * @template T - Type of items to search
 * @param items - Array of items to filter
 * @param searchFields - Fields to search in (supports dot notation)
 * @param customSearch - Optional custom search function (overrides default fuzzy search)
 * @returns Object with query state, setter, and filtered items
 */
export function useSearch<T>(
  items: T[],
  searchFields: string[],
  customSearch?: (query: string, items: T[]) => T[]
): UseSearchReturn<T> {
  const [query, setQuery] = useState('');

  // Debounce query to avoid expensive filtering on every keystroke
  const debouncedQuery = useDebounce(query, 300);

  // Filter items based on debounced query
  const filteredItems = useMemo(() => {
    if (!debouncedQuery.trim()) {
      return items;
    }

    if (customSearch) {
      return customSearch(debouncedQuery, items);
    }

    return fuzzySearch(items, debouncedQuery, searchFields);
  }, [items, debouncedQuery, searchFields, customSearch]);

  return {
    query,
    setQuery,
    filteredItems,
  };
}
