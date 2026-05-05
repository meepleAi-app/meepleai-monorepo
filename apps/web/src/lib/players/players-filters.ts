/**
 * Pure helpers for the /players v2 surface (Wave 4 D1).
 *
 * Mirrors the lib/games/library-filters.ts pattern from Wave B.1 — no React,
 * no API client, exclusively transforms over PlayerListItem / gamePlayCounts
 * Record so it stays unit-testable in isolation.
 *
 * Schema reality: the /players page actually displays games-played-by-user,
 * transformed from `gamePlayCounts: Record<gameName, count>` on
 * PlayerStatistics. Each row is a `PlayerListItem` where `displayName = gameName`
 * (anti-pattern carryover from v1 — see issue body for backend redesign followup).
 *
 * Wave 4 D1 = visual upgrade only. No semantic changes to this data shape.
 */

/** A single row in the /players list (game-as-player anti-pattern from v1). */
export interface PlayerListItem {
  /** URL-safe slug derived from gameName. */
  id: string;
  /** Human-readable label (equals gameName — v1 carryover). */
  displayName: string;
  /** The original board game name from `gamePlayCounts` Record key. */
  gameName: string;
  /** Number of sessions played for this game. */
  playCount: number;
}

/** Active filter state for the /players surface. */
export interface PlayersFilterInput {
  /** Case-insensitive gameName substring search. Empty string = no filter. */
  search: string;
}

/**
 * Applies all active filters to the item list.
 * Returns the same reference if no filter is active (empty/whitespace search).
 */
export function applyPlayersFilters(
  items: ReadonlyArray<PlayerListItem>,
  filters: PlayersFilterInput
): ReadonlyArray<PlayerListItem> {
  const search = filters.search.trim().toLowerCase();
  if (search === '') return items;
  return items.filter(item => item.gameName.toLowerCase().includes(search));
}

/**
 * Transforms a `gamePlayCounts` Record from PlayerStatistics into a sorted
 * list of PlayerListItem rows, ordered by playCount descending.
 *
 * Mirrors the v1 page.tsx transform logic to preserve visual parity.
 */
export function transformStatsToItems(
  gamePlayCounts: Record<string, number>
): ReadonlyArray<PlayerListItem> {
  return Object.entries(gamePlayCounts)
    .map(([gameName, count]) => ({
      id: gameName.toLowerCase().replace(/\s+/g, '-'),
      displayName: gameName,
      gameName,
      playCount: count,
    }))
    .sort((a, b) => b.playCount - a.playCount);
}

/**
 * Returns true when at least one filter is actively narrowing the result set.
 * Used to distinguish `empty` from `filtered-empty` in the FSM.
 */
export function hasPlayersFilters(filters: PlayersFilterInput): boolean {
  return filters.search.trim().length > 0;
}
