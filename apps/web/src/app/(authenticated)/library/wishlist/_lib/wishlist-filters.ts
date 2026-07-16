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
 * Case-insensitively matches a raw `priority` string against a known
 * `Priority` bucket. Returns `undefined` when the value isn't one of
 * high/medium/low — the wishlist DTO only types `priority` as `z.string()`,
 * not an enum, so a genuinely malformed value is possible and must be
 * distinguishable from a real `'medium'`.
 */
function matchPriority(value: string): Priority | undefined {
  const normalized = value.toLowerCase();
  if (normalized === 'high' || normalized === 'medium' || normalized === 'low') return normalized;
  return undefined;
}

/**
 * Normalizes a wishlist item's raw `priority` string (case-insensitive) into
 * a known `Priority` bucket, for ranking/filtering purposes. Falls back to
 * `'medium'` for unrecognized values so a stray/unexpected value never
 * crashes `sortItems('priority')` ranking or the priority-filter match in
 * `filterItems`.
 *
 * NOTE: `computeStats.priorityCounts` deliberately does NOT use this
 * fallback (it calls `matchPriority` directly) — an unrecognized value is
 * excluded from the displayed counts rather than silently inflating the
 * `medium` bucket.
 */
function normalizePriority(item: WishlistItemDto): Priority {
  return matchPriority(item.priority) ?? 'medium';
}

/**
 * Resolves the display name used for search matching in `filterItems`
 * (falls back to the game catalog map when the wishlist item has no
 * denormalized `gameName`).
 *
 * NOT used by `sortItems`'s `'alpha'` branch: that sorts on `item.gameName`
 * directly and never consults `gameNameMap`, so an item with no
 * denormalized name sorts as `''` there regardless of what this function
 * would resolve. Callers that need alpha-sort to reflect catalog-resolved
 * names must pre-resolve them before calling `sortItems`.
 */
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

/**
 * Header stats for the wishlist page: totals, high-priority count, sum of
 * target prices, and per-bucket priority counts.
 *
 * A `priority` value that doesn't match high/medium/low (case-insensitive)
 * is excluded from `priorityCounts`/`highCount` — it is NOT folded into
 * `medium`. This keeps the displayed counts an honest reflection of known
 * buckets; `total` still reflects every item regardless of its priority.
 */
export function computeStats(items: WishlistItemDto[]): {
  total: number;
  highCount: number;
  totalSpend: number;
  priorityCounts: Record<Priority, number>;
} {
  const priorityCounts: Record<Priority, number> = { high: 0, medium: 0, low: 0 };
  let totalSpend = 0;

  for (const item of items) {
    const bucket = matchPriority(item.priority);
    if (bucket) priorityCounts[bucket] += 1;
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
