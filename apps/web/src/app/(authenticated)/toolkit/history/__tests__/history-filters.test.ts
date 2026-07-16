/**
 * history-filters — unit tests for the pure filter/sort/paginate helpers
 * that drive the /toolkit/history table (Issue #3006, Task A2).
 *
 * All date-dependent assertions use a fixed `NOW` — never `Date.now()` /
 * `new Date()` (argless) — so the suite is deterministic regardless of
 * when/where it runs.
 */

import { describe, expect, it } from 'vitest';

import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

import {
  countActiveFilters,
  filterRows,
  paginate,
  parseWinScore,
  sortRows,
  toHistoryRow,
  type HistoryFilterState,
  type HistoryRow,
} from '../_lib/history-filters';

const NOW = new Date('2026-07-15T12:00:00Z');

const GAME_NAME_MAP = new Map<string, string>([
  ['game-1', 'Wingspan'],
  ['game-2', 'Catan'],
]);

const UNKNOWN_LABEL = 'Unknown game';

function makeDto(overrides: Partial<GameSessionDto> = {}): GameSessionDto {
  return {
    id: 'session-1',
    gameId: 'game-1',
    status: 'Completed',
    startedAt: '2026-07-10T10:00:00Z',
    completedAt: '2026-07-10T12:00:00Z',
    playerCount: 2,
    players: [
      { playerName: 'Alice', playerOrder: 0, color: null },
      { playerName: 'Bob', playerOrder: 1, color: null },
    ],
    winnerName: 'Alice',
    notes: null,
    durationMinutes: 60,
    scoringType: null,
    scoreData: null,
    turnOrderType: null,
    ...overrides,
  };
}

function baseFilterState(overrides: Partial<HistoryFilterState> = {}): HistoryFilterState {
  return {
    search: '',
    gameIds: [],
    winners: [],
    datePreset: 'all',
    sort: 'recent',
    ...overrides,
  };
}

// ─── parseWinScore ──────────────────────────────────────────────────────────

describe('parseWinScore', () => {
  it('returns the max value from a Record<string, number> scoreData payload', () => {
    const dto = makeDto({ scoreData: JSON.stringify({ Alice: 42, Bob: 30 }) });
    expect(parseWinScore(dto)).toBe(42);
  });

  it('returns the max value from a number[] scoreData payload', () => {
    const dto = makeDto({ scoreData: JSON.stringify([10, 20, 30]) });
    expect(parseWinScore(dto)).toBe(30);
  });

  it('returns null when scoreData is null', () => {
    expect(parseWinScore(makeDto({ scoreData: null }))).toBeNull();
  });

  it('returns null when scoreData is undefined', () => {
    expect(parseWinScore(makeDto({ scoreData: undefined }))).toBeNull();
  });

  it('returns null when scoreData is an empty string', () => {
    expect(parseWinScore(makeDto({ scoreData: '' }))).toBeNull();
  });

  it('returns null when scoreData is invalid JSON', () => {
    expect(parseWinScore(makeDto({ scoreData: '{not valid json' }))).toBeNull();
  });

  it('returns null when scoreData parses to an empty object', () => {
    expect(parseWinScore(makeDto({ scoreData: '{}' }))).toBeNull();
  });

  it('returns null when scoreData parses to an empty array', () => {
    expect(parseWinScore(makeDto({ scoreData: '[]' }))).toBeNull();
  });

  it('ignores non-numeric values in the payload', () => {
    const dto = makeDto({ scoreData: JSON.stringify({ Alice: 42, Bob: 'DNF' }) });
    expect(parseWinScore(dto)).toBe(42);
  });
});

// ─── toHistoryRow ───────────────────────────────────────────────────────────

describe('toHistoryRow', () => {
  it('maps a dto to a HistoryRow using the game name map', () => {
    const row = toHistoryRow(makeDto(), GAME_NAME_MAP, UNKNOWN_LABEL);
    expect(row).toEqual<HistoryRow>({
      id: 'session-1',
      gameId: 'game-1',
      gameName: 'Wingspan',
      startedAt: '2026-07-10T10:00:00Z',
      durationMinutes: 60,
      playerNames: ['Alice', 'Bob'],
      playerCount: 2,
      winnerName: 'Alice',
      isCoop: false,
      winScore: null,
      notes: null,
    });
  });

  it('falls back to the unknown label when gameId is missing from the map', () => {
    const row = toHistoryRow(makeDto({ gameId: 'ghost-game' }), GAME_NAME_MAP, UNKNOWN_LABEL);
    expect(row.gameName).toBe(UNKNOWN_LABEL);
  });

  it('derives isCoop=true when winnerName is null and there is more than 1 player', () => {
    const row = toHistoryRow(makeDto({ winnerName: null }), GAME_NAME_MAP, UNKNOWN_LABEL);
    expect(row.isCoop).toBe(true);
  });

  it('derives isCoop=false for a single-player session with no winner', () => {
    const row = toHistoryRow(
      makeDto({
        winnerName: null,
        playerCount: 1,
        players: [{ playerName: 'Alice', playerOrder: 0, color: null }],
      }),
      GAME_NAME_MAP,
      UNKNOWN_LABEL
    );
    expect(row.isCoop).toBe(false);
  });

  it('derives isCoop=false when there is a winner, even with multiple players', () => {
    const row = toHistoryRow(makeDto({ winnerName: 'Bob' }), GAME_NAME_MAP, UNKNOWN_LABEL);
    expect(row.isCoop).toBe(false);
  });

  it('parses winScore from scoreData', () => {
    const row = toHistoryRow(
      makeDto({ scoreData: JSON.stringify({ Alice: 42, Bob: 30 }) }),
      GAME_NAME_MAP,
      UNKNOWN_LABEL
    );
    expect(row.winScore).toBe(42);
  });
});

// ─── filterRows ─────────────────────────────────────────────────────────────

describe('filterRows', () => {
  const rows: HistoryRow[] = [
    toHistoryRow(
      makeDto({
        id: 'r1',
        gameId: 'game-1',
        startedAt: '2026-07-10T10:00:00Z', // 5 days before NOW -> within last30
        winnerName: 'Alice',
        players: [
          { playerName: 'Alice', playerOrder: 0, color: null },
          { playerName: 'Bob', playerOrder: 1, color: null },
        ],
        durationMinutes: 60,
        scoreData: JSON.stringify({ Alice: 42, Bob: 30 }),
      }),
      GAME_NAME_MAP,
      UNKNOWN_LABEL
    ),
    toHistoryRow(
      makeDto({
        id: 'r2',
        gameId: 'game-2',
        startedAt: '2026-05-01T10:00:00Z', // ~75 days before NOW -> within last90, outside last30
        winnerName: null,
        playerCount: 3,
        players: [
          { playerName: 'Carol', playerOrder: 0, color: null },
          { playerName: 'Dave', playerOrder: 1, color: null },
          { playerName: 'Eve', playerOrder: 2, color: null },
        ],
        durationMinutes: 120,
        scoreData: null,
      }),
      GAME_NAME_MAP,
      UNKNOWN_LABEL
    ),
    toHistoryRow(
      makeDto({
        id: 'r3',
        gameId: 'game-1',
        startedAt: '2024-01-01T10:00:00Z', // well outside lastYear
        winnerName: 'Charlie',
        players: [
          { playerName: 'Charlie', playerOrder: 0, color: null },
          { playerName: 'Dana', playerOrder: 1, color: null },
        ],
        durationMinutes: 45,
        scoreData: JSON.stringify([10, 20, 30]),
      }),
      GAME_NAME_MAP,
      UNKNOWN_LABEL
    ),
  ];

  it('matches search against gameName case-insensitively', () => {
    const result = filterRows(rows, baseFilterState({ search: 'wingspan' }), NOW);
    expect(result.map(r => r.id)).toEqual(['r1', 'r3']);
  });

  it('matches search against winnerName case-insensitively', () => {
    const result = filterRows(rows, baseFilterState({ search: 'CHARLIE' }), NOW);
    expect(result.map(r => r.id)).toEqual(['r3']);
  });

  it('matches search against player names case-insensitively', () => {
    const result = filterRows(rows, baseFilterState({ search: 'eve' }), NOW);
    expect(result.map(r => r.id)).toEqual(['r2']);
  });

  it('returns no rows when search matches nothing', () => {
    const result = filterRows(rows, baseFilterState({ search: 'nonexistent-term' }), NOW);
    expect(result).toEqual([]);
  });

  it('filters by gameIds', () => {
    const result = filterRows(rows, baseFilterState({ gameIds: ['game-2'] }), NOW);
    expect(result.map(r => r.id)).toEqual(['r2']);
  });

  it('filters by winners, matching the exact winnerName', () => {
    const result = filterRows(rows, baseFilterState({ winners: ['Alice'] }), NOW);
    expect(result.map(r => r.id)).toEqual(['r1']);
  });

  it("filters by winners using '__nowin__' to match winnerName === null", () => {
    const result = filterRows(rows, baseFilterState({ winners: ['__nowin__'] }), NOW);
    expect(result.map(r => r.id)).toEqual(['r2']);
  });

  it('filters by winners with multiple values (OR semantics)', () => {
    const result = filterRows(rows, baseFilterState({ winners: ['Alice', '__nowin__'] }), NOW);
    expect(result.map(r => r.id)).toEqual(['r1', 'r2']);
  });

  it('datePreset last30 excludes rows started more than 30 days ago', () => {
    const result = filterRows(rows, baseFilterState({ datePreset: 'last30' }), NOW);
    expect(result.map(r => r.id)).toEqual(['r1']);
  });

  it('datePreset last90 includes rows within 90 days but excludes older ones', () => {
    const result = filterRows(rows, baseFilterState({ datePreset: 'last90' }), NOW);
    expect(result.map(r => r.id)).toEqual(['r1', 'r2']);
  });

  it('datePreset lastYear excludes rows started more than a year ago', () => {
    const result = filterRows(rows, baseFilterState({ datePreset: 'lastYear' }), NOW);
    expect(result.map(r => r.id)).toEqual(['r1', 'r2']);
  });

  it('datePreset custom uses dateFrom/dateTo bounds (inclusive range)', () => {
    const result = filterRows(
      rows,
      baseFilterState({ datePreset: 'custom', dateFrom: '2026-04-01', dateTo: '2026-06-01' }),
      NOW
    );
    expect(result.map(r => r.id)).toEqual(['r2']);
  });

  it('datePreset all applies no date filtering', () => {
    const result = filterRows(rows, baseFilterState({ datePreset: 'all' }), NOW);
    expect(result.map(r => r.id)).toEqual(['r1', 'r2', 'r3']);
  });

  it('combines multiple active filters (search + gameIds + datePreset)', () => {
    const result = filterRows(
      rows,
      baseFilterState({ search: 'alice', gameIds: ['game-1'], datePreset: 'last30' }),
      NOW
    );
    expect(result.map(r => r.id)).toEqual(['r1']);
  });

  it('returns all rows when no filters are active', () => {
    const result = filterRows(rows, baseFilterState(), NOW);
    expect(result.map(r => r.id)).toEqual(['r1', 'r2', 'r3']);
  });
});

// ─── filterRows — date range edge cases (Task A7 prerequisite fix) ────────

describe('filterRows date range edge cases', () => {
  it('datePreset custom includes a session started later the same day as dateTo (end-of-day upper bound)', () => {
    const sameDayLateRow = toHistoryRow(
      makeDto({ id: 'same-day', startedAt: '2026-06-01T22:30:00Z' }),
      GAME_NAME_MAP,
      UNKNOWN_LABEL
    );

    const result = filterRows(
      [sameDayLateRow],
      baseFilterState({ datePreset: 'custom', dateFrom: '2026-05-01', dateTo: '2026-06-01' }),
      NOW
    );

    expect(result.map(r => r.id)).toEqual(['same-day']);
  });

  it('datePreset custom excludes a session started the day after dateTo', () => {
    const nextDayRow = toHistoryRow(
      makeDto({ id: 'next-day', startedAt: '2026-06-02T00:30:00Z' }),
      GAME_NAME_MAP,
      UNKNOWN_LABEL
    );

    const result = filterRows(
      [nextDayRow],
      baseFilterState({ datePreset: 'custom', dateFrom: '2026-05-01', dateTo: '2026-06-01' }),
      NOW
    );

    expect(result).toEqual([]);
  });

  it('datePreset last30 excludes a session with a future startedAt beyond now', () => {
    // NOW = 2026-07-15T12:00:00Z; a session "started" 5 days in the future
    // is within the last-30-days lower bound but must still be excluded by
    // the (now) upper bound.
    const futureRow = toHistoryRow(
      makeDto({ id: 'future', startedAt: '2026-07-20T00:00:00Z' }),
      GAME_NAME_MAP,
      UNKNOWN_LABEL
    );

    const result = filterRows([futureRow], baseFilterState({ datePreset: 'last30' }), NOW);

    expect(result).toEqual([]);
  });
});

// ─── sortRows ───────────────────────────────────────────────────────────────

describe('sortRows', () => {
  const rows: HistoryRow[] = [
    toHistoryRow(
      makeDto({
        id: 'a',
        startedAt: '2026-06-01T00:00:00Z',
        durationMinutes: 30,
        scoreData: JSON.stringify({ x: 10 }),
      }),
      GAME_NAME_MAP,
      UNKNOWN_LABEL
    ),
    toHistoryRow(
      makeDto({
        id: 'b',
        startedAt: '2026-07-01T00:00:00Z',
        durationMinutes: 90,
        scoreData: JSON.stringify({ x: 50 }),
      }),
      GAME_NAME_MAP,
      UNKNOWN_LABEL
    ),
    toHistoryRow(
      makeDto({
        id: 'c',
        startedAt: '2026-05-01T00:00:00Z',
        durationMinutes: 60,
        scoreData: null,
      }),
      GAME_NAME_MAP,
      UNKNOWN_LABEL
    ),
  ];

  it('sorts by recent (startedAt desc)', () => {
    expect(sortRows(rows, 'recent').map(r => r.id)).toEqual(['b', 'a', 'c']);
  });

  it('sorts by oldest (startedAt asc)', () => {
    expect(sortRows(rows, 'oldest').map(r => r.id)).toEqual(['c', 'a', 'b']);
  });

  it('sorts by longest (durationMinutes desc)', () => {
    expect(sortRows(rows, 'longest').map(r => r.id)).toEqual(['b', 'c', 'a']);
  });

  it('sorts by score (winScore desc, nulls last)', () => {
    expect(sortRows(rows, 'score').map(r => r.id)).toEqual(['b', 'a', 'c']);
  });

  it('does not mutate the input array', () => {
    const original = rows.map(r => r.id);
    sortRows(rows, 'recent');
    expect(rows.map(r => r.id)).toEqual(original);
  });
});

// ─── paginate ───────────────────────────────────────────────────────────────

describe('paginate', () => {
  const items = Array.from({ length: 45 }, (_, i) => i);

  it('returns the first page (page 1, pageSize 20)', () => {
    expect(paginate(items, 1, 20)).toEqual(items.slice(0, 20));
  });

  it('returns items 20..39 for page 2, pageSize 20', () => {
    expect(paginate(items, 2, 20)).toEqual(Array.from({ length: 20 }, (_, i) => i + 20));
  });

  it('returns the remaining partial page', () => {
    expect(paginate(items, 3, 20)).toEqual([40, 41, 42, 43, 44]);
  });

  it('returns an empty array when the page is beyond the data', () => {
    expect(paginate(items, 10, 20)).toEqual([]);
  });
});

// ─── countActiveFilters ─────────────────────────────────────────────────────

describe('countActiveFilters', () => {
  it('returns 0 when no filters are active', () => {
    expect(countActiveFilters(baseFilterState())).toBe(0);
  });

  it('counts search + 1 game + custom date as 3', () => {
    const count = countActiveFilters(
      baseFilterState({ search: 'wingspan', gameIds: ['game-1'], datePreset: 'custom' })
    );
    expect(count).toBe(3);
  });

  it('counts winners as an active filter', () => {
    expect(countActiveFilters(baseFilterState({ winners: ['Alice'] }))).toBe(1);
  });

  it('does not count sort as an active filter', () => {
    expect(countActiveFilters(baseFilterState({ sort: 'longest' }))).toBe(0);
  });

  it('does not count whitespace-only search as active', () => {
    expect(countActiveFilters(baseFilterState({ search: '   ' }))).toBe(0);
  });

  it('counts multiple gameIds/winners as a single active filter each', () => {
    const count = countActiveFilters(
      baseFilterState({ gameIds: ['game-1', 'game-2'], winners: ['Alice', 'Bob'] })
    );
    expect(count).toBe(2);
  });
});
