/**
 * useRecentLibraryGames — restituisce fino a N giochi della libreria ordinati
 * per recency: prima i giochi visitati di recente (Zustand recents store),
 * poi fallback a recently-added.
 *
 * Usato dal `GamesRecentRail` widget nel Games Hub per il flusso "serata di
 * gioco" (entry point ≤2 tap fino alla chat in-game).
 *
 * Spec: docs/superpowers/specs/2026-05-09-game-night-user-flow-design.md §G3
 */
import { useMemo } from 'react';

import { useLibrary, useRecentlyAddedGames } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { useRecentsStore } from '@/stores/use-recents';

export interface UseRecentLibraryGamesResult {
  readonly entries: readonly UserLibraryEntry[];
  readonly isLoading: boolean;
  readonly isError: boolean;
}

const LIBRARY_FETCH_SIZE = 50;

export function useRecentLibraryGames(limit: number = 5): UseRecentLibraryGamesResult {
  const libraryQuery = useLibrary({ page: 1, pageSize: LIBRARY_FETCH_SIZE });
  const recentlyAdded = useRecentlyAddedGames(limit);

  const recents = useRecentsStore(state => state.items);

  return useMemo<UseRecentLibraryGamesResult>(() => {
    const isLoading = libraryQuery.isLoading;
    const isError = libraryQuery.isError && recents.length === 0;

    const libIndex = new Map<string, UserLibraryEntry>();
    for (const entry of libraryQuery.data?.items ?? []) {
      libIndex.set(entry.gameId, entry);
    }

    const seen = new Set<string>();
    const result: UserLibraryEntry[] = [];

    for (const recent of recents) {
      if (recent.entity !== 'game') continue;
      const entry = libIndex.get(recent.id);
      if (!entry || seen.has(entry.gameId)) continue;
      result.push(entry);
      seen.add(entry.gameId);
      if (result.length >= limit) break;
    }

    if (result.length < limit) {
      for (const entry of recentlyAdded.data?.items ?? []) {
        if (seen.has(entry.gameId)) continue;
        result.push(entry);
        seen.add(entry.gameId);
        if (result.length >= limit) break;
      }
    }

    return { entries: result, isLoading, isError };
  }, [libraryQuery.data, libraryQuery.isLoading, libraryQuery.isError, recentlyAdded.data, recents, limit]);
}
