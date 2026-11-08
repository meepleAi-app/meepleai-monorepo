import { describe, expect, it } from 'vitest';

import type { LiveSessionDto, LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import {
  canRecordScores,
  cellValue,
  deriveRound,
  finalScores,
  scoringPlayers,
} from '../round-scores';

function player(id: string, overrides: Partial<LiveSessionPlayerDto> = {}): LiveSessionPlayerDto {
  return {
    id,
    userId: null,
    displayName: id,
    avatarUrl: null,
    color: 'Red',
    role: 'Player',
    teamId: null,
    totalScore: 0,
    currentRank: 1,
    joinedAt: '2026-10-14T10:00:00Z',
    isActive: true,
    ...overrides,
  };
}

function session(overrides: Partial<LiveSessionDto> = {}): LiveSessionDto {
  return {
    id: 'live-1',
    sessionCode: 'ABC123',
    gameId: null,
    gameName: 'Azul',
    gameSlug: 'azul',
    createdByUserId: 'user-1',
    status: 'InProgress',
    visibility: 'Private',
    groupId: null,
    createdAt: '2026-10-14T10:00:00Z',
    startedAt: '2026-10-14T10:00:00Z',
    pausedAt: null,
    completedAt: null,
    updatedAt: '2026-10-14T10:00:00Z',
    lastSavedAt: null,
    currentTurnIndex: 1,
    currentTurnPlayerId: null,
    agentMode: 'None',
    notes: null,
    players: [],
    teams: [],
    roundScores: [],
    scoringConfig: { enabledDimensions: ['punti'], dimensionUnits: {} },
    ...overrides,
  } as LiveSessionDto;
}

describe('deriveRound — #4156 D6', () => {
  it('is 1 before the start and without players', () => {
    expect(deriveRound(0, 3)).toBe(1);
    expect(deriveRound(5, 0)).toBe(1);
  });

  it('counts full laps of the table: a round is one turn per player', () => {
    // 3 players: turns 1–3 are round 1, turns 4–6 round 2, turn 7 opens round 3.
    expect([1, 2, 3].map(turn => deriveRound(turn, 3))).toEqual([1, 1, 1]);
    expect([4, 5, 6].map(turn => deriveRound(turn, 3))).toEqual([2, 2, 2]);
    expect(deriveRound(7, 3)).toBe(3);
  });

  it('is the turn itself in a solo game', () => {
    expect(deriveRound(4, 1)).toBe(4);
  });
});

describe('scoringPlayers', () => {
  it('keeps the active players and leaves out spectators and inactive players', () => {
    const live = session({
      players: [
        player('marco'),
        player('ospite', { role: 'Spectator' }),
        player('uscito', { isActive: false }),
        player('giulia', { role: 'Host' }),
      ],
    });
    expect(scoringPlayers(live).map(p => p.id)).toEqual(['marco', 'giulia']);
  });
});

describe('cellValue', () => {
  const live = session({
    roundScores: [
      { playerId: 'marco', round: 1, dimension: 'Punti', value: 10, unit: null, recordedAt: '' },
      { playerId: 'marco', round: 2, dimension: 'punti', value: 4, unit: null, recordedAt: '' },
    ],
  });

  it('finds the cell of a player, round and dimension, ignoring the case of the dimension', () => {
    expect(cellValue(live, 'marco', 1, 'punti')).toBe(10);
    expect(cellValue(live, 'marco', 2, 'PUNTI')).toBe(4);
  });

  it('is undefined for a cell nobody scored', () => {
    expect(cellValue(live, 'marco', 3, 'punti')).toBeUndefined();
    expect(cellValue(live, 'giulia', 1, 'punti')).toBeUndefined();
  });
});

describe('canRecordScores', () => {
  it('allows writes only while the game runs', () => {
    expect(canRecordScores(session({ status: 'InProgress' }))).toBe(true);
    expect(canRecordScores(session({ status: 'Paused' }))).toBe(true);
    expect(canRecordScores(session({ status: 'Created' }))).toBe(false);
    expect(canRecordScores(session({ status: 'Completed' }))).toBe(false);
  });
});

describe('finalScores', () => {
  it('reports the backend totals and the player alone in the lead as the winner', () => {
    const live = session({
      players: [
        player('marco', { totalScore: 10 }),
        player('giulia', { totalScore: 30 }),
        player('ospite', { role: 'Spectator', totalScore: 99 }),
      ],
    });
    expect(finalScores(live)).toEqual([
      { playerName: 'marco', score: 10, isWinner: false },
      { playerName: 'giulia', score: 30, isWinner: true },
    ]);
  });

  it('has no winner on a tie for the lead', () => {
    const live = session({
      players: [player('marco', { totalScore: 20 }), player('giulia', { totalScore: 20 })],
    });
    expect(finalScores(live).some(score => score.isWinner)).toBe(false);
  });

  it('is empty without scoring players', () => {
    expect(finalScores(session())).toEqual([]);
  });
});
