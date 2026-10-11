/**
 * Game Search Hook with Debounce
 *
 * Searches games on the server with a 300ms debounce, from one of two sources:
 * - `catalog` (default): the game catalog, from 2 characters on;
 * - `library`: the user's library (#4187). The server filters by title, so a game
 *   past the first page is still found. With an empty query it returns the first
 *   page sorted by title, so a picker has something to show before typing. Private
 *   games are not part of the library endpoint.
 *
 * The query key carries source, text and page size: an answer for an older text
 * never overwrites the current one.
 *
 * Issue #4273: Game Search Autocomplete
 */

import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { useDebounce } from '@/components/ui/data-display/entity-list-view/hooks/use-debounce';
import { api } from '@/lib/api';

export type GameSearchSource = 'catalog' | 'library';

export interface GameSearchResult {
  id: string;
  name: string;
  source: 'library' | 'catalog' | 'private';
  imageUrl?: string;
}

export interface UseGameSearchOptions {
  /** Where to search (default: `catalog`). */
  source?: GameSearchSource;
  /** Debounce delay in milliseconds (default: 300). */
  debounceMs?: number;
  /** Library page size (default: 20). Clamped to 1..100, what the server returns at most. */
  limit?: number;
  /** Whether to run the query (default: true). */
  enabled?: boolean;
}

/** `GET /api/v1/library` accepts a page size from 1 to 100 (UserLibraryRepository). */
const LIBRARY_MAX_PAGE_SIZE = 100;

/**
 * Search games with a debounced query
 *
 * @param query - Search query string
 * @param options - Source, debounce, page size and enabled flag
 * @returns Query result with games array
 */
export function useGameSearch(query: string, options: UseGameSearchOptions = {}) {
  const { source = 'catalog', debounceMs = 300, limit = 20, enabled = true } = options;
  const debouncedQuery = useDebounce(query, debounceMs);
  const pageSize = Math.min(Math.max(Math.trunc(limit), 1), LIBRARY_MAX_PAGE_SIZE);
  const isLibrary = source === 'library';

  return useQuery({
    queryKey: isLibrary
      ? ['games', 'search', 'library', debouncedQuery, pageSize]
      : ['games', 'search', 'catalog', debouncedQuery],
    queryFn: async (): Promise<GameSearchResult[]> => {
      if (isLibrary) {
        const search = debouncedQuery.trim();
        const response = await api.library.getLibrary({
          search: search || undefined,
          page: 1,
          pageSize,
          sortBy: 'title',
          // The endpoint sorts descending unless told otherwise (`sortDescending ?? true`).
          sortDescending: false,
        });
        return response.items.map(entry => ({
          id: entry.gameId,
          name: entry.gameTitle,
          source: 'library' as const,
          imageUrl: entry.gameImageUrl ?? undefined,
        }));
      }

      if (!debouncedQuery || debouncedQuery.length < 2) {
        return [];
      }

      const response = await fetch(
        `/api/v1/games?search=${encodeURIComponent(debouncedQuery)}&pageSize=20`
      );

      if (!response.ok) {
        throw new Error('Failed to search games');
      }

      const data = await response.json();
      // Backend returns { games: [{ id, title, imageUrl, ... }], total, page, ... }
      const games = data.games ?? data;
      return (games as Array<{ id: string; title?: string; name?: string; imageUrl?: string }>).map(
        g => ({
          id: g.id,
          name: g.title ?? g.name ?? 'Unknown',
          source: 'catalog' as const,
          imageUrl: g.imageUrl ?? undefined,
        })
      );
    },
    enabled: enabled && (isLibrary || debouncedQuery.length >= 2),
    // The library keeps the previous list on screen while the next text loads, so a
    // picker does not flash "no games" at every keystroke.
    placeholderData: isLibrary ? keepPreviousData : undefined,
    staleTime: 5 * 60 * 1000, // Cache for 5 minutes
  });
}
