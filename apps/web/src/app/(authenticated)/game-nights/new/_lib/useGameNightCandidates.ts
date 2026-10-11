/**
 * The candidate games of step 4 of `game-nights/new` (#4187).
 *
 * The wizard used to ask for the first 50 library games and offer them as the whole choice,
 * so a game past that page could not be proposed. Now:
 * - the library is searched on the server (`search`, ILike on the title), sorted by title;
 * - a chosen game that the current search does not show is read by id and kept at the top,
 *   so it stays visible and deselectable, and the RSVP preview can still name it.
 *
 * Library entries carry the shared-catalog id (`gameId`), the same id `useSharedGamesByIds`
 * reads. Private games are not part of the library endpoint.
 */

import { useMemo } from 'react';

import type { GameCandidateOption } from '@/components/features/game-night-create';
import { useDebounce } from '@/components/ui/data-display/entity-list-view/hooks/use-debounce';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useSharedGamesByIds } from '@/hooks/queries/useSharedGames';

/** How many library games the picker lists at once. */
const CANDIDATE_PAGE_SIZE = 50;

export interface UseGameNightCandidatesArgs {
  /** What the user typed in the picker's search box. */
  readonly search: string;
  /** The games already chosen for the night. */
  readonly selectedIds: readonly string[];
  /** Whether to search the library (only on step 4). Chosen games are named regardless. */
  readonly enabled: boolean;
}

export interface GameNightCandidates {
  /** Chosen games the current search does not show, first; then the search results. */
  readonly games: readonly GameCandidateOption[];
  /** True while a search is running. */
  readonly isSearching: boolean;
}

export function useGameNightCandidates({
  search,
  selectedIds,
  enabled,
}: UseGameNightCandidatesArgs): GameNightCandidates {
  const term = useDebounce(search, 300).trim();

  const libraryQuery = useLibrary(
    {
      search: term || undefined,
      page: 1,
      pageSize: CANDIDATE_PAGE_SIZE,
      sortBy: 'title',
      // The endpoint sorts descending unless told otherwise (`sortDescending ?? true`).
      sortDescending: false,
    },
    enabled,
    true
  );

  const results = useMemo<GameCandidateOption[]>(
    () =>
      (libraryQuery.data?.items ?? []).map(entry => ({
        id: entry.gameId,
        title: entry.gameTitle,
        imageUrl: entry.gameImageUrl,
        minPlayers: entry.minPlayers ?? null,
        maxPlayers: entry.maxPlayers ?? null,
        playingTimeMinutes: entry.playingTimeMinutes ?? null,
      })),
    [libraryQuery.data]
  );

  // Until the search has an answer (data or error) nobody is "missing" yet: reading every
  // chosen game by id on the first render would fetch the ones the search is about to show.
  const searchSettled = !enabled || !libraryQuery.isPending;
  const missingIds = useMemo(() => {
    if (!searchSettled) return [];
    const shown = new Set(results.map(g => g.id));
    return selectedIds.filter(id => !shown.has(id));
  }, [searchSettled, results, selectedIds]);

  const { gamesById } = useSharedGamesByIds(missingIds, missingIds.length > 0);

  const games = useMemo<GameCandidateOption[]>(() => {
    const pinned: GameCandidateOption[] = [];
    for (const id of missingIds) {
      const game = gamesById[id];
      if (!game) continue;
      pinned.push({
        id: game.id,
        title: game.title,
        // `coverUrl` is the only cover the frontend may render (BGG asset ban, #2123).
        imageUrl: game.coverUrl ?? null,
        // The catalog uses 0 for "unknown".
        minPlayers: game.minPlayers || null,
        maxPlayers: game.maxPlayers || null,
        playingTimeMinutes: game.playingTimeMinutes || null,
      });
    }
    return [...pinned, ...results];
  }, [missingIds, gamesById, results]);

  return { games, isSearching: libraryQuery.isFetching };
}
