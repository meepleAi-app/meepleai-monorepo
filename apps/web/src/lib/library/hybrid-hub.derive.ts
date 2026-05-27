/**
 * deriveHybridItems — pure merge/filter/sort function for the hybrid hub.
 * Phase 1 (Issue #1591) foundation; Phase 2's `LibraryHub` will call this
 * with the 5 mapped per-entity arrays once the orchestration hooks are wired.
 *
 * `HybridHubTab` is a Phase-1-internal literal; the top-level `LibraryEntityKey`
 * stays at `'all' | 'kb' | 'loaned'` until Phase 2's tab expansion + STATO
 * chip ship together (the two changes only make sense in one atomic step).
 */

import type { HybridHubEntity, HybridHubItem } from './hybrid-hub.types';
import type { LibrarySortKey } from './library-filters';

export type HybridHubTab = 'all' | 'games' | 'agents' | 'kb' | 'sessions' | 'chat';

export interface HybridHubSources {
  readonly games: readonly HybridHubItem[];
  readonly agents: readonly HybridHubItem[];
  readonly kb: readonly HybridHubItem[];
  readonly sessions: readonly HybridHubItem[];
  readonly chat: readonly HybridHubItem[];
}

const TAB_TO_ENTITY: Record<Exclude<HybridHubTab, 'all'>, HybridHubEntity> = {
  games: 'game',
  agents: 'agent',
  kb: 'kb',
  sessions: 'session',
  chat: 'chat',
};

const STATE_ORDER: Record<string, number> = {
  Owned: 0,
  Nuovo: 1,
  InPrestito: 2,
  Wishlist: 3,
};

export function deriveHybridItems(
  sources: HybridHubSources,
  tab: HybridHubTab,
  query: string,
  sort: LibrarySortKey
): HybridHubItem[] {
  const merged: HybridHubItem[] = [
    ...sources.games,
    ...sources.agents,
    ...sources.kb,
    ...sources.sessions,
    ...sources.chat,
  ];
  const tabFiltered =
    tab === 'all' ? merged : merged.filter(it => it.entity === TAB_TO_ENTITY[tab]);
  const queryFiltered = matchHybridQuery(tabFiltered, query);
  return sortHybridItems(queryFiltered, sort);
}

function matchHybridQuery(items: readonly HybridHubItem[], query: string): HybridHubItem[] {
  const trimmed = query.trim().toLowerCase();
  if (trimmed.length === 0) return [...items];
  return items.filter(
    it =>
      it.title.toLowerCase().includes(trimmed) ||
      (it.subtitle?.toLowerCase().includes(trimmed) ?? false)
  );
}

function sortHybridItems(items: readonly HybridHubItem[], sort: LibrarySortKey): HybridHubItem[] {
  const copy = [...items];
  switch (sort) {
    case 'title':
      return copy.sort((a, b) =>
        a.title.localeCompare(b.title, undefined, { sensitivity: 'base' })
      );
    case 'rating':
      return copy.sort(compareRating);
    case 'state':
      return copy.sort(compareState);
    case 'recent':
    default:
      return copy.sort((a, b) => Date.parse(b.updatedAt) - Date.parse(a.updatedAt));
  }
}

function compareRating(a: HybridHubItem, b: HybridHubItem): number {
  const aRating = a.entity === 'game' ? a.rating : undefined;
  const bRating = b.entity === 'game' ? b.rating : undefined;
  if (aRating == null && bRating == null) return 0;
  if (aRating == null) return 1;
  if (bRating == null) return -1;
  return bRating - aRating;
}

function compareState(a: HybridHubItem, b: HybridHubItem): number {
  const aIsGame = a.entity === 'game';
  const bIsGame = b.entity === 'game';
  if (aIsGame && !bIsGame) return -1;
  if (!aIsGame && bIsGame) return 1;
  if (!aIsGame && !bIsGame) return 0;
  // both games
  const aOrder = STATE_ORDER[(a as { state?: string }).state ?? ''] ?? 99;
  const bOrder = STATE_ORDER[(b as { state?: string }).state ?? ''] ?? 99;
  return aOrder - bOrder;
}
