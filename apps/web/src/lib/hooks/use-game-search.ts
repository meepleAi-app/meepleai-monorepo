/**
 * Game Search Hook with Debounce
 *
 * Searches across all game sources (UserLibrary + SharedGames + PrivateGames)
 * with 300ms debounce to prevent excessive API calls.
 *
 * Issue #4273: Game Search Autocomplete
 */

import { useQuery } from '@tanstack/react-query';

import { useDebounce } from '@/components/ui/data-display/entity-list-view/hooks/use-debounce';

export interface GameSearchResult {
  id: string;
  name: string;
  source: 'library' | 'catalog' | 'private';
  imageUrl?: string;
}

/**
 * Search games with debounced query
 *
 * @param query - Search query string
 * @param debounceMs - Debounce delay in milliseconds (default: 300)
 * @returns Query result with games array
 */
export function useGameSearch(query: string, debounceMs = 300) {
  const debouncedQuery = useDebounce(query, debounceMs);

  return useQuery({
    queryKey: ['games', 'search', debouncedQuery],
    queryFn: async () => {
      if (!debouncedQuery || debouncedQuery.length < 2) {
        return [];
      }

      const response = await fetch(
        `/api/v1/games/search?q=${encodeURIComponent(debouncedQuery)}`
      );

      if (!response.ok) {
        throw new Error('Failed to search games');
      }

      const data = await response.json();
      return data as GameSearchResult[];
    },
    enabled: debouncedQuery.length >= 2,
    staleTime: 5 * 60 * 1000, // Cache for 5 minutes
  });
}
