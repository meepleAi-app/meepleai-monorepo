/**
 * history-filters — pure filter/sort/paginate helpers for /toolkit/history.
 *
 * Issue #3006 (Task A2). The backend `GET` history endpoint returns a plain
 * array with no search/winner-filter/sort support, so the entire history
 * table experience (search, filter, sort, pagination) is computed
 * client-side from these pure functions. Later tasks (A4-A9) build the UI
 * and data-fetching layer on top of these types/functions — keep the
 * exported names/signatures stable.
 *
 * No React, no i18n, no `Date.now()` — every date-relative computation takes
 * an explicit `now: Date` argument so callers (and tests) stay deterministic.
 */

import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

/** Sentinel value used in `HistoryFilterState.winners` to match sessions with no recorded winner (co-op / draw). */
export const NO_WINNER_FILTER_VALUE = '__nowin__';

const MS_PER_DAY = 24 * 60 * 60 * 1000;

export type HistorySort = 'recent' | 'oldest' | 'longest' | 'score';

export type DateRangePreset = 'all' | 'last30' | 'last90' | 'lastYear' | 'custom';

export interface HistoryFilterState {
  search: string;
  gameIds: string[];
  winners: string[];
  datePreset: DateRangePreset;
  dateFrom?: string;
  dateTo?: string;
  sort: HistorySort;
}

/** View-model row consumed by the history table — one per `GameSessionDto`. */
export interface HistoryRow {
  id: string;
  gameId: string;
  gameName: string;
  startedAt: string;
  durationMinutes: number;
  playerNames: string[];
  playerCount: number;
  winnerName: string | null;
  isCoop: boolean;
  winScore: number | null;
  notes: string | null;
}

/**
 * Parses the polymorphic `scoreData` JSON payload and returns the highest
 * numeric score found, or `null` when the payload is missing, invalid, or
 * has no numeric values. Supports two shapes:
 *   - `Record<string, number>` (e.g. `{ "Alice": 42, "Bob": 30 }`) — uses the values
 *   - `number[]` (e.g. `[10, 20, 30]`)
 */
export function parseWinScore(dto: GameSessionDto): number | null {
  if (!dto.scoreData) return null;

  let parsed: unknown;
  try {
    parsed = JSON.parse(dto.scoreData);
  } catch {
    return null;
  }

  let values: number[];
  if (Array.isArray(parsed)) {
    values = parsed.filter((v): v is number => typeof v === 'number');
  } else if (parsed !== null && typeof parsed === 'object') {
    values = Object.values(parsed as Record<string, unknown>).filter(
      (v): v is number => typeof v === 'number'
    );
  } else {
    return null;
  }

  return values.length > 0 ? Math.max(...values) : null;
}

/** Maps a `GameSessionDto` to the view-model `HistoryRow` consumed by the history table. */
export function toHistoryRow(
  dto: GameSessionDto,
  gameNameMap: Map<string, string>,
  unknownLabel: string
): HistoryRow {
  const playerNames = [...dto.players]
    .sort((a, b) => a.playerOrder - b.playerOrder)
    .map(p => p.playerName);

  return {
    id: dto.id,
    gameId: dto.gameId,
    gameName: gameNameMap.get(dto.gameId) ?? unknownLabel,
    startedAt: dto.startedAt,
    durationMinutes: dto.durationMinutes,
    playerNames,
    playerCount: dto.playerCount,
    winnerName: dto.winnerName,
    // Heuristic: a session with no recorded winner and more than one player is
    // treated as a co-operative session (a single-player session with no
    // winner is just "not finished scoring", not co-op).
    isCoop: dto.winnerName == null && dto.players.length > 1,
    winScore: parseWinScore(dto),
    notes: dto.notes,
  };
}

/**
 * Resolves a `[start, end]` date bound (inclusive) for the given preset, or
 * `[null, null]` when unbounded.
 *
 * Two fixes applied here (Issue #3006, Task A7 prerequisite):
 *   1. Relative presets (`last30`/`last90`/`lastYear`) are capped at `now` —
 *      without an upper bound, a future-dated session (e.g. a clock-skewed
 *      or accidentally-future `startedAt`) would pass the filter.
 *   2. A custom `dateTo` (a `YYYY-MM-DD` string) resolves to the END of that
 *      day (`23:59:59.999`), not its start — otherwise `new Date(dateTo)`
 *      parses to `00:00:00.000Z` and same-day sessions are excluded from
 *      the range they were explicitly selected into.
 */
function resolveDateRange(f: HistoryFilterState, now: Date): [Date | null, Date | null] {
  switch (f.datePreset) {
    case 'last30':
      return [new Date(now.getTime() - 30 * MS_PER_DAY), now];
    case 'last90':
      return [new Date(now.getTime() - 90 * MS_PER_DAY), now];
    case 'lastYear':
      return [new Date(now.getTime() - 365 * MS_PER_DAY), now];
    case 'custom':
      return [
        f.dateFrom ? new Date(f.dateFrom) : null,
        f.dateTo ? new Date(`${f.dateTo}T23:59:59.999Z`) : null,
      ];
    case 'all':
    default:
      return [null, null];
  }
}

/** Filters rows by search text, game, winner, and date range. All criteria are AND-combined; values within a single criterion (gameIds/winners) are OR-combined. */
export function filterRows(rows: HistoryRow[], f: HistoryFilterState, now: Date): HistoryRow[] {
  const search = f.search.trim().toLowerCase();
  const gameIdSet = new Set(f.gameIds);
  const winnerSet = new Set(f.winners);
  const [rangeStart, rangeEnd] = resolveDateRange(f, now);

  return rows.filter(row => {
    if (search) {
      const haystack = [row.gameName, row.winnerName ?? '', ...row.playerNames]
        .join(' ')
        .toLowerCase();
      if (!haystack.includes(search)) return false;
    }

    if (gameIdSet.size > 0 && !gameIdSet.has(row.gameId)) return false;

    if (winnerSet.size > 0) {
      const matchesNoWinner = row.winnerName == null && winnerSet.has(NO_WINNER_FILTER_VALUE);
      const matchesWinner = row.winnerName != null && winnerSet.has(row.winnerName);
      if (!matchesNoWinner && !matchesWinner) return false;
    }

    if (rangeStart || rangeEnd) {
      const startedAt = new Date(row.startedAt).getTime();
      if (rangeStart && startedAt < rangeStart.getTime()) return false;
      if (rangeEnd && startedAt > rangeEnd.getTime()) return false;
    }

    return true;
  });
}

/** Returns a new, sorted array (input is never mutated). */
export function sortRows(rows: HistoryRow[], sort: HistorySort): HistoryRow[] {
  const sorted = [...rows];

  switch (sort) {
    case 'recent':
      sorted.sort((a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime());
      break;
    case 'oldest':
      sorted.sort((a, b) => new Date(a.startedAt).getTime() - new Date(b.startedAt).getTime());
      break;
    case 'longest':
      sorted.sort((a, b) => b.durationMinutes - a.durationMinutes);
      break;
    case 'score':
      sorted.sort((a, b) => {
        if (a.winScore == null && b.winScore == null) return 0;
        if (a.winScore == null) return 1;
        if (b.winScore == null) return -1;
        return b.winScore - a.winScore;
      });
      break;
    default:
      break;
  }

  return sorted;
}

/** Slices `items` to the given 1-indexed `page` of size `pageSize`. */
export function paginate<T>(items: T[], page: number, pageSize: number): T[] {
  const start = (page - 1) * pageSize;
  return items.slice(start, start + pageSize);
}

/** Counts how many filter *categories* are active (search, game, winner, date) — value counts within a category (e.g. 2 selected games) still count as 1. `sort` is never counted. */
export function countActiveFilters(f: HistoryFilterState): number {
  let count = 0;
  if (f.search.trim() !== '') count += 1;
  if (f.gameIds.length > 0) count += 1;
  if (f.winners.length > 0) count += 1;
  if (f.datePreset !== 'all') count += 1;
  return count;
}
