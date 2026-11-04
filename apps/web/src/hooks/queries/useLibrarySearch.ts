/**
 * Search the user's library on the server (#4187).
 *
 * For pickers that show whole library entries (cover, publisher, players). They used to
 * download one page and filter it on the client, so a game past that page could not be
 * found. The server filters by title (`search`, ILike in UserLibraryRepository), sorted by
 * title A→Z. With an empty text it returns the first page, and `totalCount` is the size
 * of the whole library (or of the matches), not of the page.
 *
 * The previous page stays on screen while the next text loads (`keepPrevious`), so a list
 * does not empty out at every keystroke; `resultsMatchQuery` tells when it is outdated.
 */

import { useDebounce } from '@/components/ui/data-display/entity-list-view/hooks/use-debounce';
import { useLibrary } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

/** `GET /api/v1/library` returns at most 100 entries per page (UserLibraryRepository). */
const LIBRARY_MAX_PAGE_SIZE = 100;

export interface UseLibrarySearchOptions {
  /** Whether to query (default: true). */
  readonly enabled?: boolean;
  /** Entries per page (default: 50), clamped to 1..100. */
  readonly pageSize?: number;
  /** Debounce delay in milliseconds (default: 300). */
  readonly debounceMs?: number;
  /**
   * Only games with (true) or without (false) a ready KB, filtered on the server (#4227):
   * `totalCount` then counts those games, not the whole library.
   */
  readonly hasKb?: boolean;
}

export interface LibrarySearchResult {
  readonly items: readonly UserLibraryEntry[];
  /** Matches on the server for the current text: the whole library when it is empty. */
  readonly totalCount: number;
  /** True only while the first page has not arrived yet. */
  readonly isLoading: boolean;
  /** False while the list on screen belongs to an older text. */
  readonly resultsMatchQuery: boolean;
  /** The trimmed text the server was asked for. */
  readonly term: string;
}

export function useLibrarySearch(
  search: string,
  { enabled = true, pageSize = 50, debounceMs = 300, hasKb }: UseLibrarySearchOptions = {}
): LibrarySearchResult {
  const debounced = useDebounce(search, debounceMs);
  const term = debounced.trim();
  const size = Math.min(Math.max(Math.trunc(pageSize), 1), LIBRARY_MAX_PAGE_SIZE);

  const query = useLibrary(
    {
      search: term || undefined,
      page: 1,
      pageSize: size,
      hasKb,
      sortBy: 'title',
      // The endpoint sorts descending unless told otherwise (`sortDescending ?? true`).
      sortDescending: false,
    },
    enabled,
    true
  );

  return {
    items: query.data?.items ?? [],
    totalCount: query.data?.totalCount ?? 0,
    isLoading: query.isLoading,
    resultsMatchQuery: debounced === search && !query.isPlaceholderData,
    term,
  };
}
