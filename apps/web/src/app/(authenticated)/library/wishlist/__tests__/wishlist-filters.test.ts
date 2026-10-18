/**
 * wishlist-filters — unit tests for the pure filter/sort/stats helpers
 * that drive the /library/wishlist page (Issue #3007, Task B1).
 *
 * All ordering assertions use explicit ISO `addedAt` timestamps — never
 * `Date.now()` / `new Date()` (argless) — so the suite is deterministic
 * regardless of when/where it runs.
 */

import { describe, expect, it } from 'vitest';

import type { WishlistItemDto } from '@/lib/api/schemas/wishlist.schemas';

import {
  computeStats,
  countActiveFilters,
  filterItems,
  normalizePriorityString,
  sortItems,
  PRIORITY_RANK,
  type WishlistFilterState,
} from '../_lib/wishlist-filters';

function makeItem(overrides: Partial<WishlistItemDto> = {}): WishlistItemDto {
  return {
    id: 'item-1',
    userId: 'user-1',
    gameId: 'game-1',
    gameName: undefined,
    priority: 'medium',
    targetPrice: null,
    notes: null,
    addedAt: '2026-07-01T10:00:00Z',
    updatedAt: null,
    visibility: 'Private',
    ...overrides,
  };
}

function baseFilterState(overrides: Partial<WishlistFilterState> = {}): WishlistFilterState {
  return {
    search: '',
    priorities: [],
    sort: 'recent',
    ...overrides,
  };
}

// ─── PRIORITY_RANK ──────────────────────────────────────────────────────────

describe('PRIORITY_RANK', () => {
  it('ranks high < medium < low', () => {
    expect(PRIORITY_RANK.high).toBeLessThan(PRIORITY_RANK.medium);
    expect(PRIORITY_RANK.medium).toBeLessThan(PRIORITY_RANK.low);
  });
});

// ─── normalizePriorityString ────────────────────────────────────────────────

describe('normalizePriorityString', () => {
  it('lowercases and matches a known priority value', () => {
    expect(normalizePriorityString('HIGH')).toBe('high');
  });

  it('falls back to medium for an unrecognized value', () => {
    expect(normalizePriorityString('urgent')).toBe('medium');
  });

  it('falls back to medium for undefined', () => {
    expect(normalizePriorityString(undefined)).toBe('medium');
  });

  it('falls back to medium for an empty string', () => {
    expect(normalizePriorityString('')).toBe('medium');
  });
});

// ─── filterItems ────────────────────────────────────────────────────────────

describe('filterItems', () => {
  it('matches search against item.gameName (case-insensitive)', () => {
    const items = [
      makeItem({ id: 'a', gameName: 'Wingspan' }),
      makeItem({ id: 'b', gameName: 'Catan' }),
    ];
    const result = filterItems(items, baseFilterState({ search: 'wing' }));
    expect(result.map(i => i.id)).toEqual(['a']);
  });

  it('matches an item without gameName (game no longer in the catalog) on its notes only', () => {
    const items = [
      makeItem({ id: 'a', gameName: undefined, notes: 'regalo per Catan' }),
      makeItem({ id: 'b', gameName: undefined, notes: null }),
    ];
    const result = filterItems(items, baseFilterState({ search: 'catan' }));
    expect(result.map(i => i.id)).toEqual(['a']);
  });

  it('matches search against notes (case-insensitive)', () => {
    const items = [
      makeItem({ id: 'a', gameName: 'Wingspan', notes: 'Waiting for Black Friday sale' }),
      makeItem({ id: 'b', gameName: 'Catan', notes: null }),
    ];
    const result = filterItems(items, baseFilterState({ search: 'BLACK FRIDAY' }));
    expect(result.map(i => i.id)).toEqual(['a']);
  });

  it('returns all items when search is empty', () => {
    const items = [makeItem({ id: 'a' }), makeItem({ id: 'b' })];
    const result = filterItems(items, baseFilterState());
    expect(result).toHaveLength(2);
  });

  it('filters by priorities (multi-select, OR-combined)', () => {
    const items = [
      makeItem({ id: 'a', priority: 'high' }),
      makeItem({ id: 'b', priority: 'medium' }),
      makeItem({ id: 'c', priority: 'low' }),
    ];
    const result = filterItems(items, baseFilterState({ priorities: ['high', 'low'] }));
    expect(result.map(i => i.id)).toEqual(['a', 'c']);
  });

  it('treats an empty priorities array as "match all"', () => {
    const items = [makeItem({ id: 'a', priority: 'high' }), makeItem({ id: 'b', priority: 'low' })];
    const result = filterItems(items, baseFilterState({ priorities: [] }));
    expect(result).toHaveLength(2);
  });

  it('normalizes a stray-cased priority when matching the priorities filter', () => {
    const items = [makeItem({ id: 'a', priority: 'HIGH' })];
    const result = filterItems(items, baseFilterState({ priorities: ['high'] }));
    expect(result.map(i => i.id)).toEqual(['a']);
  });

  it('AND-combines search and priorities', () => {
    const items = [
      makeItem({ id: 'a', gameName: 'Wingspan', priority: 'high' }),
      makeItem({ id: 'b', gameName: 'Wingspan', priority: 'low' }),
      makeItem({ id: 'c', gameName: 'Catan', priority: 'high' }),
    ];
    const result = filterItems(
      items,
      baseFilterState({ search: 'wingspan', priorities: ['high'] })
    );
    expect(result.map(i => i.id)).toEqual(['a']);
  });
});

// ─── sortItems ──────────────────────────────────────────────────────────────

describe('sortItems', () => {
  it('sorts by priority rank, then addedAt desc within the same rank', () => {
    const items = [
      makeItem({ id: 'low-old', priority: 'low', addedAt: '2026-07-01T00:00:00Z' }),
      makeItem({ id: 'high-old', priority: 'high', addedAt: '2026-07-01T00:00:00Z' }),
      makeItem({ id: 'high-new', priority: 'high', addedAt: '2026-07-05T00:00:00Z' }),
      makeItem({ id: 'medium', priority: 'medium', addedAt: '2026-07-03T00:00:00Z' }),
    ];
    const result = sortItems(items, 'priority');
    expect(result.map(i => i.id)).toEqual(['high-new', 'high-old', 'medium', 'low-old']);
  });

  it('normalizes a stray-cased priority when ranking', () => {
    const items = [
      makeItem({ id: 'a', priority: 'Low', addedAt: '2026-07-01T00:00:00Z' }),
      makeItem({ id: 'b', priority: 'HIGH', addedAt: '2026-07-01T00:00:00Z' }),
    ];
    const result = sortItems(items, 'priority');
    expect(result.map(i => i.id)).toEqual(['b', 'a']);
  });

  it('ranks an unrecognized priority value as medium (never crashes)', () => {
    const items = [
      makeItem({ id: 'high', priority: 'high', addedAt: '2026-07-01T00:00:00Z' }),
      makeItem({ id: 'unrecognized', priority: 'urgent', addedAt: '2026-07-01T00:00:00Z' }),
      makeItem({ id: 'low', priority: 'low', addedAt: '2026-07-01T00:00:00Z' }),
    ];
    expect(() => sortItems(items, 'priority')).not.toThrow();
    // 'urgent' falls back to the medium rank (1), so it sorts between high and low.
    const result = sortItems(items, 'priority');
    expect(result.map(i => i.id)).toEqual(['high', 'unrecognized', 'low']);
  });

  it('sorts by recent (addedAt desc)', () => {
    const items = [
      makeItem({ id: 'a', addedAt: '2026-07-01T00:00:00Z' }),
      makeItem({ id: 'b', addedAt: '2026-07-10T00:00:00Z' }),
      makeItem({ id: 'c', addedAt: '2026-07-05T00:00:00Z' }),
    ];
    const result = sortItems(items, 'recent');
    expect(result.map(i => i.id)).toEqual(['b', 'c', 'a']);
  });

  it('sorts by oldest (addedAt asc)', () => {
    const items = [
      makeItem({ id: 'a', addedAt: '2026-07-01T00:00:00Z' }),
      makeItem({ id: 'b', addedAt: '2026-07-10T00:00:00Z' }),
      makeItem({ id: 'c', addedAt: '2026-07-05T00:00:00Z' }),
    ];
    const result = sortItems(items, 'oldest');
    expect(result.map(i => i.id)).toEqual(['a', 'c', 'b']);
  });

  it('sorts alphabetically by gameName', () => {
    const items = [
      makeItem({ id: 'a', gameName: 'Wingspan' }),
      makeItem({ id: 'b', gameName: 'Azul' }),
      makeItem({ id: 'c', gameName: 'Catan' }),
    ];
    const result = sortItems(items, 'alpha');
    expect(result.map(i => i.id)).toEqual(['b', 'c', 'a']);
  });

  it('sorts by price desc, with null targetPrice last', () => {
    const items = [
      makeItem({ id: 'no-price', targetPrice: null }),
      makeItem({ id: 'cheap', targetPrice: 10 }),
      makeItem({ id: 'expensive', targetPrice: 50 }),
    ];
    const result = sortItems(items, 'price');
    expect(result.map(i => i.id)).toEqual(['expensive', 'cheap', 'no-price']);
  });

  it('does not mutate the input array', () => {
    const items = [
      makeItem({ id: 'a', addedAt: '2026-07-01T00:00:00Z' }),
      makeItem({ id: 'b', addedAt: '2026-07-10T00:00:00Z' }),
    ];
    const original = [...items];
    sortItems(items, 'recent');
    expect(items).toEqual(original);
  });

  it('is stable (order-preserving) for two items that both have a null targetPrice', () => {
    const items = [
      makeItem({ id: 'first', targetPrice: null }),
      makeItem({ id: 'second', targetPrice: null }),
    ];
    const result = sortItems(items, 'price');
    expect(result.map(i => i.id)).toEqual(['first', 'second']);
  });
});

// ─── computeStats ───────────────────────────────────────────────────────────

describe('computeStats', () => {
  it('returns zeroed stats for an empty list', () => {
    expect(computeStats([])).toEqual({
      total: 0,
      highCount: 0,
      totalSpend: 0,
      priorityCounts: { high: 0, medium: 0, low: 0 },
    });
  });

  it('counts total, highCount, and priorityCounts per bucket', () => {
    const items = [
      makeItem({ id: 'a', priority: 'high' }),
      makeItem({ id: 'b', priority: 'high' }),
      makeItem({ id: 'c', priority: 'medium' }),
      makeItem({ id: 'd', priority: 'low' }),
    ];
    const stats = computeStats(items);
    expect(stats.total).toBe(4);
    expect(stats.highCount).toBe(2);
    expect(stats.priorityCounts).toEqual({ high: 2, medium: 1, low: 1 });
  });

  it('sums targetPrice, skipping null entries', () => {
    const items = [
      makeItem({ id: 'a', targetPrice: 10.5 }),
      makeItem({ id: 'b', targetPrice: null }),
      makeItem({ id: 'c', targetPrice: 24.5 }),
    ];
    expect(computeStats(items).totalSpend).toBe(35);
  });

  it('normalizes a stray-cased priority when bucketing', () => {
    const items = [makeItem({ id: 'a', priority: 'HIGH' })];
    const stats = computeStats(items);
    expect(stats.highCount).toBe(1);
    expect(stats.priorityCounts).toEqual({ high: 1, medium: 0, low: 0 });
  });

  it('counts valid lowercase priority values correctly', () => {
    const items = [
      makeItem({ id: 'a', priority: 'high' }),
      makeItem({ id: 'b', priority: 'medium' }),
      makeItem({ id: 'c', priority: 'medium' }),
      makeItem({ id: 'd', priority: 'low' }),
    ];
    const stats = computeStats(items);
    expect(stats.priorityCounts).toEqual({ high: 1, medium: 2, low: 1 });
  });

  it('excludes an unrecognized priority value from priorityCounts/highCount instead of folding it into medium', () => {
    const items = [
      makeItem({ id: 'a', priority: 'high' }),
      makeItem({ id: 'b', priority: 'urgent' }),
      makeItem({ id: 'c', priority: '' }),
    ];
    const stats = computeStats(items);
    // total still reflects every item, regardless of priority validity.
    expect(stats.total).toBe(3);
    // only the genuinely 'high' item is counted — 'urgent' and '' are excluded,
    // not silently counted as 'medium'.
    expect(stats.highCount).toBe(1);
    expect(stats.priorityCounts).toEqual({ high: 1, medium: 0, low: 0 });
  });
});

// ─── countActiveFilters ─────────────────────────────────────────────────────

describe('countActiveFilters', () => {
  it('returns 0 when no filters are active', () => {
    expect(countActiveFilters(baseFilterState())).toBe(0);
  });

  it('counts a non-empty search as 1', () => {
    expect(countActiveFilters(baseFilterState({ search: 'wingspan' }))).toBe(1);
  });

  it('treats a whitespace-only search as inactive', () => {
    expect(countActiveFilters(baseFilterState({ search: '   ' }))).toBe(0);
  });

  it('counts any non-empty priorities selection as 1, regardless of size', () => {
    expect(countActiveFilters(baseFilterState({ priorities: ['high'] }))).toBe(1);
    expect(countActiveFilters(baseFilterState({ priorities: ['high', 'low'] }))).toBe(1);
  });

  it('sums independent active categories', () => {
    expect(countActiveFilters(baseFilterState({ search: 'wingspan', priorities: ['high'] }))).toBe(
      2
    );
  });
});
