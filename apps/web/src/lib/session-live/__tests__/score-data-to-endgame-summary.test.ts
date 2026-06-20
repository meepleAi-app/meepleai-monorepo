/**
 * mapScoreDataToEndgameSummary unit tests — Issue #2431.
 *
 * Pure adapter that derives EndgameDialog.finalScores (winner badge + sorted
 * display) from the polymorphic store shape. Replaces the hardcoded
 * `isWinner: false` mapping at SessionLiveView.tsx (#2389 Block B leftover).
 *
 * Winner rules per variant:
 *   Points     → first player with max points (tie-break by player order).
 *   BinaryWin  → every player with isWinner: true (co-op / multi-winner OK).
 *   Ranking    → position === 1 is the winner.
 *   Objectives → first player with max completedObjectives.length (tie-break).
 *
 * Score field semantics (Dialog already sorts winners first, then score DESC):
 *   Points     → raw points.
 *   BinaryWin  → 0 (winner status drives visibility; score is unused).
 *   Ranking    → (players.length - position + 1) so winner is highest, last is 1.
 *   Objectives → completedObjectives.length.
 */

import { describe, expect, it } from 'vitest';

import { mapScoreDataToEndgameSummary } from '@/lib/session-live/score-data-to-endgame-summary';
import type { ScoreDataByType } from '@/components/sessions/score-strategies/types';

const PLAYERS = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Anna', displayName: 'Anna B.' },
  { id: 'p3', name: 'Luca' },
] as const;

// ─── Null gates ───────────────────────────────────────────────────────────────

describe('mapScoreDataToEndgameSummary — null gates', () => {
  it('returns empty array when scoringType is null', () => {
    const result = mapScoreDataToEndgameSummary(null, { scores: [] }, PLAYERS);
    expect(result).toEqual([]);
  });

  it('returns empty array when scoreData is null', () => {
    const result = mapScoreDataToEndgameSummary('Points', null, PLAYERS);
    expect(result).toEqual([]);
  });

  it('returns empty array when both null', () => {
    const result = mapScoreDataToEndgameSummary(null, null, PLAYERS);
    expect(result).toEqual([]);
  });
});

// ─── Points ───────────────────────────────────────────────────────────────────

describe('mapScoreDataToEndgameSummary — Points', () => {
  it('marks the top scorer as winner', () => {
    const scoreData: ScoreDataByType['Points'] = {
      scores: [
        { playerId: 'p1', points: 7 },
        { playerId: 'p2', points: 12 },
        { playerId: 'p3', points: 3 },
      ],
    };
    const result = mapScoreDataToEndgameSummary('Points', scoreData, PLAYERS);
    expect(result).toEqual([
      { playerName: 'Marco', score: 7, isWinner: false },
      { playerName: 'Anna B.', score: 12, isWinner: true },
      { playerName: 'Luca', score: 3, isWinner: false },
    ]);
  });

  it('tie-breaks by player order (first with max wins)', () => {
    const scoreData: ScoreDataByType['Points'] = {
      scores: [
        { playerId: 'p1', points: 10 },
        { playerId: 'p2', points: 10 },
        { playerId: 'p3', points: 5 },
      ],
    };
    const result = mapScoreDataToEndgameSummary('Points', scoreData, PLAYERS);
    expect(result.filter(r => r.isWinner)).toEqual([
      { playerName: 'Marco', score: 10, isWinner: true },
    ]);
  });

  it('pads missing player with score 0 (non-winner)', () => {
    const scoreData: ScoreDataByType['Points'] = {
      scores: [{ playerId: 'p1', points: 5 }],
    };
    const result = mapScoreDataToEndgameSummary('Points', scoreData, PLAYERS);
    expect(result).toEqual([
      { playerName: 'Marco', score: 5, isWinner: true },
      { playerName: 'Anna B.', score: 0, isWinner: false },
      { playerName: 'Luca', score: 0, isWinner: false },
    ]);
  });

  it('treats all-zero (no one scored) as no winner', () => {
    const scoreData: ScoreDataByType['Points'] = {
      scores: [
        { playerId: 'p1', points: 0 },
        { playerId: 'p2', points: 0 },
        { playerId: 'p3', points: 0 },
      ],
    };
    const result = mapScoreDataToEndgameSummary('Points', scoreData, PLAYERS);
    expect(result.every(r => !r.isWinner)).toBe(true);
  });
});

// ─── BinaryWin ────────────────────────────────────────────────────────────────

describe('mapScoreDataToEndgameSummary — BinaryWin', () => {
  it('mirrors isWinner from scoreData (single winner)', () => {
    const scoreData: ScoreDataByType['BinaryWin'] = {
      results: [
        { playerId: 'p1', isWinner: false },
        { playerId: 'p2', isWinner: true },
        { playerId: 'p3', isWinner: false },
      ],
    };
    const result = mapScoreDataToEndgameSummary('BinaryWin', scoreData, PLAYERS);
    expect(result).toEqual([
      { playerName: 'Marco', score: 0, isWinner: false },
      { playerName: 'Anna B.', score: 0, isWinner: true },
      { playerName: 'Luca', score: 0, isWinner: false },
    ]);
  });

  it('supports multi-winner (co-op)', () => {
    const scoreData: ScoreDataByType['BinaryWin'] = {
      results: [
        { playerId: 'p1', isWinner: true },
        { playerId: 'p2', isWinner: true },
        { playerId: 'p3', isWinner: true },
      ],
    };
    const result = mapScoreDataToEndgameSummary('BinaryWin', scoreData, PLAYERS);
    expect(result.every(r => r.isWinner)).toBe(true);
  });

  it('defaults missing player to isWinner: false', () => {
    const scoreData: ScoreDataByType['BinaryWin'] = {
      results: [{ playerId: 'p1', isWinner: true }],
    };
    const result = mapScoreDataToEndgameSummary('BinaryWin', scoreData, PLAYERS);
    expect(result[1]).toEqual({ playerName: 'Anna B.', score: 0, isWinner: false });
  });
});

// ─── Ranking ──────────────────────────────────────────────────────────────────

describe('mapScoreDataToEndgameSummary — Ranking', () => {
  it('marks position 1 as winner with highest synthetic score', () => {
    const scoreData: ScoreDataByType['Ranking'] = {
      positions: [
        { playerId: 'p1', position: 2 },
        { playerId: 'p2', position: 1 },
        { playerId: 'p3', position: 3 },
      ],
    };
    const result = mapScoreDataToEndgameSummary('Ranking', scoreData, PLAYERS);
    // score = players.length - position + 1 → winner (pos 1) gets N (=3), last (pos N) gets 1
    expect(result).toEqual([
      { playerName: 'Marco', score: 2, isWinner: false }, // pos 2 → 3-2+1=2
      { playerName: 'Anna B.', score: 3, isWinner: true }, // pos 1 → 3-1+1=3
      { playerName: 'Luca', score: 1, isWinner: false }, // pos 3 → 3-3+1=1
    ]);
  });

  it('defaults missing player to last position (score 1, not winner)', () => {
    const scoreData: ScoreDataByType['Ranking'] = {
      positions: [{ playerId: 'p2', position: 1 }],
    };
    const result = mapScoreDataToEndgameSummary('Ranking', scoreData, PLAYERS);
    expect(result[0]).toEqual({ playerName: 'Marco', score: 1, isWinner: false });
    expect(result[1]).toEqual({ playerName: 'Anna B.', score: 3, isWinner: true });
    expect(result[2]).toEqual({ playerName: 'Luca', score: 1, isWinner: false });
  });

  it('clamps out-of-range positions to [1, N] without crowning (defensive)', () => {
    // BE bug / store desync: position 0 (off-by-one) and position > N.
    // The synthetic score must stay in [1, N]; isWinner stays strict on
    // position === 1, so position 0 is NOT crowned.
    const scoreData: ScoreDataByType['Ranking'] = {
      positions: [
        { playerId: 'p1', position: 0 }, // out-of-range low
        { playerId: 'p2', position: 1 }, // real winner
        { playerId: 'p3', position: 99 }, // out-of-range high
      ],
    };
    const result = mapScoreDataToEndgameSummary('Ranking', scoreData, PLAYERS);
    expect(result[0]).toEqual({ playerName: 'Marco', score: 3, isWinner: false }); // clamped to pos 1 → score 3, but NOT winner
    expect(result[1]).toEqual({ playerName: 'Anna B.', score: 3, isWinner: true });
    expect(result[2]).toEqual({ playerName: 'Luca', score: 1, isWinner: false }); // clamped to pos N
  });
});

// ─── Objectives ───────────────────────────────────────────────────────────────

describe('mapScoreDataToEndgameSummary — Objectives', () => {
  it('marks the player with most completed objectives as winner', () => {
    const scoreData: ScoreDataByType['Objectives'] = {
      completedByPlayer: [
        { playerId: 'p1', objectives: ['A'] },
        { playerId: 'p2', objectives: ['A', 'B', 'C'] },
        { playerId: 'p3', objectives: ['A', 'B'] },
      ],
    };
    const result = mapScoreDataToEndgameSummary('Objectives', scoreData, PLAYERS);
    expect(result).toEqual([
      { playerName: 'Marco', score: 1, isWinner: false },
      { playerName: 'Anna B.', score: 3, isWinner: true },
      { playerName: 'Luca', score: 2, isWinner: false },
    ]);
  });

  it('tie-breaks by player order (first with max wins)', () => {
    const scoreData: ScoreDataByType['Objectives'] = {
      completedByPlayer: [
        { playerId: 'p1', objectives: ['A', 'B'] },
        { playerId: 'p2', objectives: ['A', 'B'] },
        { playerId: 'p3', objectives: ['A'] },
      ],
    };
    const result = mapScoreDataToEndgameSummary('Objectives', scoreData, PLAYERS);
    expect(result.filter(r => r.isWinner)).toEqual([
      { playerName: 'Marco', score: 2, isWinner: true },
    ]);
  });

  it('treats all-zero (no completion) as no winner', () => {
    const scoreData: ScoreDataByType['Objectives'] = {
      completedByPlayer: [
        { playerId: 'p1', objectives: [] },
        { playerId: 'p2', objectives: [] },
        { playerId: 'p3', objectives: [] },
      ],
    };
    const result = mapScoreDataToEndgameSummary('Objectives', scoreData, PLAYERS);
    expect(result.every(r => !r.isWinner)).toBe(true);
  });
});

// ─── displayName fallback ─────────────────────────────────────────────────────

describe('mapScoreDataToEndgameSummary — display name', () => {
  it('falls back to player.name when displayName is absent', () => {
    const scoreData: ScoreDataByType['Points'] = {
      scores: [{ playerId: 'p1', points: 1 }],
    };
    const result = mapScoreDataToEndgameSummary('Points', scoreData, PLAYERS);
    expect(result[0].playerName).toBe('Marco'); // no displayName on p1
    expect(result[1].playerName).toBe('Anna B.'); // displayName on p2
  });
});
