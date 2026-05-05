/**
 * Pure helpers for the /games library v2 surface (Issue #633, Wave B.1).
 *
 * Caso B (UserLibraryEntry lacks playCount/lastPlayedAt/totalSessions):
 *   - 'played' filter is a no-op fallback — keep all entries.
 *   - 'last-played' sort falls back to `addedAt` DESC.
 *
 * No React, no API client — exclusively transforms over the canonical
 * `UserLibraryEntry` schema so it stays unit-testable in isolation.
 */

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

export type GamesStatusKey = 'all' | 'owned' | 'wishlist' | 'played';
export type GamesSortKey = 'last-played' | 'rating' | 'title' | 'year';

export interface GamesLibraryStats {
  owned: number;
  wishlist: number;
  totalEntries: number;
  kbDocs: number;
}

export function filterByStatus(
  entries: readonly UserLibraryEntry[],
  status: GamesStatusKey
): UserLibraryEntry[] {
  switch (status) {
    case 'owned':
      return entries.filter(e => e.currentState === 'Owned');
    case 'wishlist':
      return entries.filter(e => e.currentState === 'Wishlist');
    case 'played':
      // Caso B fallback: backend lacks playCount; surface UI hides the tab.
      // Keeping a deterministic identity branch lets us flip to a real filter
      // in Caso A without breaking call-sites.
      return entries.slice();
    case 'all':
    default:
      return entries.slice();
  }
}

export function matchQuery(entry: UserLibraryEntry, query: string): boolean {
  const trimmed = query.trim().toLowerCase();
  if (trimmed.length === 0) {
    return true;
  }
  if (entry.gameTitle.toLowerCase().includes(trimmed)) {
    return true;
  }
  if (entry.gamePublisher && entry.gamePublisher.toLowerCase().includes(trimmed)) {
    return true;
  }
  if (entry.gameYearPublished != null && String(entry.gameYearPublished).includes(trimmed)) {
    return true;
  }
  return false;
}

function compareNullable(a: number | null | undefined, b: number | null | undefined): number {
  const aMissing = a == null;
  const bMissing = b == null;
  if (aMissing && bMissing) return 0;
  if (aMissing) return 1;
  if (bMissing) return -1;
  return (b as number) - (a as number);
}

export function sortLibraryEntries(
  entries: readonly UserLibraryEntry[],
  sortKey: GamesSortKey
): UserLibraryEntry[] {
  const copy = entries.slice();
  switch (sortKey) {
    case 'title':
      return copy.sort((a, b) =>
        a.gameTitle.localeCompare(b.gameTitle, undefined, { sensitivity: 'base' })
      );
    case 'rating':
      return copy.sort((a, b) => compareNullable(a.averageRating, b.averageRating));
    case 'year':
      return copy.sort((a, b) => compareNullable(a.gameYearPublished, b.gameYearPublished));
    case 'last-played':
    default:
      return copy.sort((a, b) => Date.parse(b.addedAt) - Date.parse(a.addedAt));
  }
}

export function deriveStats(entries: readonly UserLibraryEntry[]): GamesLibraryStats {
  let owned = 0;
  let wishlist = 0;
  let kbDocs = 0;
  for (const entry of entries) {
    if (entry.currentState === 'Owned') owned += 1;
    else if (entry.currentState === 'Wishlist') wishlist += 1;
    kbDocs += entry.kbCardCount;
  }
  return { owned, wishlist, totalEntries: entries.length, kbDocs };
}
