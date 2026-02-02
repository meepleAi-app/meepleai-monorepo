/**
 * useGlobalSearch Hook
 * Issue #3289 - Phase 3: GlobalSearch Component
 *
 * Manages global search state including:
 * - Search query with debounce
 * - Results from API
 * - Recent searches (localStorage)
 * - Keyboard navigation
 */

'use client';

import { useState, useCallback, useEffect, useMemo } from 'react';

import { useQuery } from '@tanstack/react-query';

import type { SearchResult, SearchResultType } from '@/components/layout/GlobalSearch/SearchResults';
import { useDebounce } from '@/hooks/useDebounce';

/**
 * Local storage key for recent searches
 */
const RECENT_SEARCHES_KEY = 'meepleai:recent-searches';
const MAX_RECENT_SEARCHES = 5;

/**
 * API response type
 */
interface SearchApiResponse {
  results: Array<{
    id: string;
    name: string;
    description?: string;
    type?: string;
  }>;
  total: number;
}

/**
 * Fetch search results from API
 */
async function fetchSearchResults(query: string): Promise<SearchResult[]> {
  if (!query || query.length < 2) {
    return [];
  }

  try {
    const response = await fetch(
      `/api/v1/shared-games/search?q=${encodeURIComponent(query)}&limit=10`
    );

    if (!response.ok) {
      throw new Error('Search failed');
    }

    const data: SearchApiResponse = await response.json();

    // Map API response to SearchResult format
    return data.results.map((item) => ({
      id: item.id,
      type: 'game' as SearchResultType,
      title: item.name,
      subtitle: item.description,
      href: `/games/${item.id}`,
    }));
  } catch {
    // Return empty results on error
    return [];
  }
}

/**
 * Get recent searches from localStorage
 */
function getRecentSearches(): string[] {
  if (typeof window === 'undefined') return [];

  try {
    const stored = localStorage.getItem(RECENT_SEARCHES_KEY);
    return stored ? JSON.parse(stored) : [];
  } catch {
    return [];
  }
}

/**
 * Save recent searches to localStorage
 */
function saveRecentSearches(searches: string[]): void {
  if (typeof window === 'undefined') return;

  try {
    localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(searches));
  } catch {
    // Ignore storage errors
  }
}

/**
 * Hook return type
 */
export interface UseGlobalSearchReturn {
  /** Current search query */
  query: string;
  /** Set search query */
  setQuery: (query: string) => void;
  /** Debounced query for API calls */
  debouncedQuery: string;
  /** Search results */
  results: SearchResult[];
  /** Whether search is loading */
  isLoading: boolean;
  /** Search error */
  error: Error | null;
  /** Recent search queries */
  recentSearches: string[];
  /** Add query to recent searches */
  addRecentSearch: (query: string) => void;
  /** Remove query from recent searches */
  removeRecentSearch: (query: string) => void;
  /** Clear all recent searches */
  clearRecentSearches: () => void;
  /** Currently highlighted result index */
  highlightedIndex: number;
  /** Set highlighted index */
  setHighlightedIndex: (index: number) => void;
  /** Move highlight up */
  highlightPrevious: () => void;
  /** Move highlight down */
  highlightNext: () => void;
  /** Get currently highlighted result */
  highlightedResult: SearchResult | null;
  /** Clear search state */
  clearSearch: () => void;
  /** Whether search is active (has query or is focused) */
  isActive: boolean;
  /** Set search active state */
  setIsActive: (active: boolean) => void;
}

/**
 * useGlobalSearch Hook
 *
 * Manages all global search state and logic.
 */
export function useGlobalSearch(): UseGlobalSearchReturn {
  // Search query state
  const [query, setQuery] = useState('');
  const debouncedQuery = useDebounce(query, 300);

  // UI state
  const [isActive, setIsActive] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(-1);

  // Recent searches state
  const [recentSearches, setRecentSearches] = useState<string[]>([]);

  // Load recent searches on mount
  useEffect(() => {
    setRecentSearches(getRecentSearches());
  }, []);

  // Fetch search results
  const {
    data: results = [],
    isLoading,
    error,
  } = useQuery({
    queryKey: ['globalSearch', debouncedQuery],
    queryFn: () => fetchSearchResults(debouncedQuery),
    enabled: debouncedQuery.length >= 2,
    staleTime: 60000, // 1 minute
  });

  // Reset highlight when results change
  useEffect(() => {
    setHighlightedIndex(-1);
  }, [results]);

  // Add to recent searches
  const addRecentSearch = useCallback((searchQuery: string) => {
    if (!searchQuery.trim()) return;

    setRecentSearches((prev) => {
      // Remove if already exists, then add to front
      const filtered = prev.filter((s) => s !== searchQuery);
      const updated = [searchQuery, ...filtered].slice(0, MAX_RECENT_SEARCHES);
      saveRecentSearches(updated);
      return updated;
    });
  }, []);

  // Remove from recent searches
  const removeRecentSearch = useCallback((searchQuery: string) => {
    setRecentSearches((prev) => {
      const updated = prev.filter((s) => s !== searchQuery);
      saveRecentSearches(updated);
      return updated;
    });
  }, []);

  // Clear all recent searches
  const clearRecentSearches = useCallback(() => {
    setRecentSearches([]);
    saveRecentSearches([]);
  }, []);

  // Highlight navigation
  const highlightPrevious = useCallback(() => {
    setHighlightedIndex((prev) => {
      if (prev <= 0) return results.length - 1;
      return prev - 1;
    });
  }, [results.length]);

  const highlightNext = useCallback(() => {
    setHighlightedIndex((prev) => {
      if (prev >= results.length - 1) return 0;
      return prev + 1;
    });
  }, [results.length]);

  // Get highlighted result
  const highlightedResult = useMemo(() => {
    if (highlightedIndex < 0 || highlightedIndex >= results.length) {
      return null;
    }
    // eslint-disable-next-line security/detect-object-injection -- highlightedIndex is validated numeric index
    return results[highlightedIndex];
  }, [highlightedIndex, results]);

  // Clear search
  const clearSearch = useCallback(() => {
    setQuery('');
    setHighlightedIndex(-1);
    setIsActive(false);
  }, []);

  return {
    query,
    setQuery,
    debouncedQuery,
    results,
    isLoading,
    error: error as Error | null,
    recentSearches,
    addRecentSearch,
    removeRecentSearch,
    clearRecentSearches,
    highlightedIndex,
    setHighlightedIndex,
    highlightPrevious,
    highlightNext,
    highlightedResult,
    clearSearch,
    isActive,
    setIsActive,
  };
}
