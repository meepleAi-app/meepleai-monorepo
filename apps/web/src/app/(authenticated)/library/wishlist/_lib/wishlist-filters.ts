/**
 * wishlist-filters — pure filter/sort/stats helpers for /library/wishlist.
 *
 * Issue #3007 (Task B1). The wishlist endpoint returns a plain array with no
 * search/priority-filter/sort support, so the entire wishlist experience
 * (search, priority filter, sort, header stats) is computed client-side from
 * these pure functions. Later tasks (B3-B6) build the UI on top of these
 * types/functions — keep the exported names/signatures stable.
 *
 * No React, no i18n.
 */

import type { WishlistItemDto } from '@/lib/api/schemas/wishlist.schemas';

export type WishlistSort = 'priority' | 'recent' | 'oldest' | 'alpha' | 'price';

export type Priority = 'high' | 'medium' | 'low';

/** Sort rank per priority bucket — lower ranks first (high wins). */
export const PRIORITY_RANK: Record<Priority, number> = { high: 0, medium: 1, low: 2 };

export interface WishlistFilterState {
  search: string;
  priorities: Priority[];
  sort: WishlistSort;
}

/**
 * Normalizes a wishlist item's raw `priority` string (case-insensitive) into
 * a known `Priority` bucket. Falls back to `'medium'` for unrecognized
 * values so a stray/unexpected value never crashes ranking or bucketing.
 */
function normalizePriority(item: WishlistItemDto): Priority {
  const value = item.priority.toLowerCase();
  if (value === 'high' || value === 'medium' || value === 'low') return value;
  return 'medium';
}

/** Resolves the display name used for search matching and alpha sort. */
function resolveGameName(item: WishlistItemDto, gameNameMap: Map<string, string>): string {
  return item.gameName ?? gameNameMap.get(item.gameId) ?? '';
}

/** Filters items by search text (gameName + notes) and priorities. All criteria are AND-combined; values within `priorities` are OR-combined. */
export function filterItems(
  items: WishlistItemDto[],
  f: WishlistFilterState,
  gameNameMap: Map<string, string>
): WishlistItemDto[] {
  const search = f.search.trim().toLowerCase();
  const prioritySet = new Set(f.priorities);

  return items.filter(item => {
    if (search) {
      const haystack = [resolveGameName(item, gameNameMap), item.notes ?? '']
        .join(' ')
        .toLowerCase();
      if (!haystack.includes(search)) return false;
    }

    if (prioritySet.size > 0 && !prioritySet.has(normalizePriority(item))) return false;

    return true;
  });
}

/** Returns a new, sorted array (input is never mutated). */
export function sortItems(items: WishlistItemDto[], sort: WishlistSort): WishlistItemDto[] {
  const sorted = [...items];

  switch (sort) {
    case 'priority':
      sorted.sort((a, b) => {
        const rankDiff = PRIORITY_RANK[normalizePriority(a)] - PRIORITY_RANK[normalizePriority(b)];
        if (rankDiff !== 0) return rankDiff;
        return new Date(b.addedAt).getTime() - new Date(a.addedAt).getTime();
      });
      break;
    case 'recent':
      sorted.sort((a, b) => new Date(b.addedAt).getTime() - new Date(a.addedAt).getTime());
      break;
    case 'oldest':
      sorted.sort((a, b) => new Date(a.addedAt).getTime() - new Date(b.addedAt).getTime());
      break;
    case 'alpha':
      sorted.sort((a, b) => (a.gameName ?? '').localeCompare(b.gameName ?? ''));
      break;
    case 'price':
      sorted.sort((a, b) => {
        if (a.targetPrice == null && b.targetPrice == null) return 0;
        if (a.targetPrice == null) return 1;
        if (b.targetPrice == null) return -1;
        return b.targetPrice - a.targetPrice;
      });
      break;
    default:
      break;
  }

  return sorted;
}

/** Header stats for the wishlist page: totals, high-priority count, sum of target prices, and per-bucket priority counts. */
export function computeStats(items: WishlistItemDto[]): {
  total: number;
  highCount: number;
  totalSpend: number;
  priorityCounts: Record<Priority, number>;
} {
  const priorityCounts: Record<Priority, number> = { high: 0, medium: 0, low: 0 };
  let totalSpend = 0;

  for (const item of items) {
    priorityCounts[normalizePriority(item)] += 1;
    if (item.targetPrice != null) totalSpend += item.targetPrice;
  }

  return {
    total: items.length,
    highCount: priorityCounts.high,
    totalSpend,
    priorityCounts,
  };
}

/** Counts how many filter *categories* are active (search, priorities). `sort` is never counted. */
export function countActiveFilters(f: WishlistFilterState): number {
  let count = 0;
  if (f.search.trim() !== '') count += 1;
  if (f.priorities.length > 0) count += 1;
  return count;
}
