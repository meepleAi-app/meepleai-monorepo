/**
 * Pure filter/sort/search helpers for the LibraryHub `games` tab (#1566).
 *
 * No React, no IO — deterministic transforms over `UserLibraryEntry[]`.
 * Maps the `sp4-games-index` mockup STATUS_OPTS / SORT_OPTS to entry fields.
 * See docs/superpowers/specs/2026-05-27-1566-library-games-tab-wireup-design.md §3.4.
 */

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import type { GamesSortKey, GamesStatusKey } from '@/lib/games/library-filters';

export function filterGamesByStatus(
  entries: readonly UserLibraryEntry[],
  status: GamesStatusKey
): readonly UserLibraryEntry[] {
  switch (status) {
    case 'owned':
      return entries.filter(e => e.currentState !== 'Wishlist');
    case 'wishlist':
      return entries.filter(e => e.currentState === 'Wishlist');
    case 'played':
      return entries.filter(e => e.timesPlayed > 0);
    case 'all':
    default:
      return entries;
  }
}

export function filterGamesByQuery(
  entries: readonly UserLibraryEntry[],
  query: string
): readonly UserLibraryEntry[] {
  const q = query.trim().toLowerCase();
  if (q === '') return entries;
  return entries.filter(e => e.gameTitle.toLowerCase().includes(q));
}

const titleCollator = new Intl.Collator(undefined, { sensitivity: 'base' });

export function sortGames(
  entries: readonly UserLibraryEntry[],
  sort: GamesSortKey
): readonly UserLibraryEntry[] {
  const copy = [...entries];
  switch (sort) {
    case 'rating':
      return copy.sort((a, b) => (b.averageRating ?? -1) - (a.averageRating ?? -1));
    case 'title':
      return copy.sort((a, b) => titleCollator.compare(a.gameTitle, b.gameTitle));
    case 'year':
      return copy.sort(
        (a, b) => (b.gameYearPublished || 0) - (a.gameYearPublished || 0)
      );
    case 'last-played':
    default:
      return copy.sort((a, b) => {
        const ta = a.lastPlayed ? Date.parse(a.lastPlayed) : 0;
        const tb = b.lastPlayed ? Date.parse(b.lastPlayed) : 0;
        return tb - ta;
      });
  }
}

export function deriveGamesTabEntries(
  entries: readonly UserLibraryEntry[],
  status: GamesStatusKey,
  query: string,
  sort: GamesSortKey
): readonly UserLibraryEntry[] {
  return sortGames(filterGamesByQuery(filterGamesByStatus(entries, status), query), sort);
}
