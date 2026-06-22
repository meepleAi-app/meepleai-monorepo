/**
 * mapScoreDataToPanelData unit tests — Issue #2389 Block B (T3).
 *
 * Pure function: no React, no hook ceremony, no store mocking required.
 * Covers null gates, 4 happy-path variants, displayName fallback,
 * missing-player padding per variant, Objectives catalogue edges,
 * and empty-players-list edges.
 */

import { describe, expect, it } from 'vitest';

import { mapScoreDataToPanelData } from '@/lib/session-live/score-data-to-panel-data';
import type { ScoreDataByType } from '@/components/sessions/score-strategies/types';

const PLAYERS = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Anna', displayName: 'Anna B.' },
] as const;

const CATALOGUE = ['Vittoria', 'Tesoro'] as const;

// ─── Null gates ───────────────────────────────────────────────────────────────

describe('mapScoreDataToPanelData — null gates', () => {
  it('returns null when scoringType is null', () => {
    const result = mapScoreDataToPanelData(null, { scores: [] }, PLAYERS);
    expect(result).toBeNull();
  });

  it('returns null when scoreData is null', () => {
    const result = mapScoreDataToPanelData('Points', null, PLAYERS);
    expect(result).toBeNull();
  });

  it('returns null when both null', () => {
    const result = mapScoreDataToPanelData(null, null, PLAYERS);
    expect(result).toBeNull();
  });
});

// ─── Happy path per variant ───────────────────────────────────────────────────

describe('mapScoreDataToPanelData — happy path', () => {
  it('maps Points correctly', () => {
    const scoreData: ScoreDataByType['Points'] = {
      scores: [
        { playerId: 'p1', points: 10 },
        { playerId: 'p2', points: 7 },
      ],
    };
    const result = mapScoreDataToPanelData('Points', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'Points',
      players: [
        { id: 'p1', displayName: 'Marco', score: 10 },
        { id: 'p2', displayName: 'Anna B.', score: 7 },
      ],
    });
  });

  it('maps BinaryWin correctly', () => {
    const scoreData: ScoreDataByType['BinaryWin'] = {
      results: [
        { playerId: 'p1', isWinner: true },
        { playerId: 'p2', isWinner: false },
      ],
    };
    const result = mapScoreDataToPanelData('BinaryWin', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'BinaryWin',
      players: [
        { id: 'p1', displayName: 'Marco', isWinner: true },
        { id: 'p2', displayName: 'Anna B.', isWinner: false },
      ],
    });
  });

  it('maps Ranking correctly', () => {
    const scoreData: ScoreDataByType['Ranking'] = {
      positions: [
        { playerId: 'p1', position: 2 },
        { playerId: 'p2', position: 1 },
      ],
    };
    const result = mapScoreDataToPanelData('Ranking', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'Ranking',
      players: [
        { id: 'p1', displayName: 'Marco', position: 2 },
        { id: 'p2', displayName: 'Anna B.', position: 1 },
      ],
    });
  });

  it('maps Objectives with catalogue', () => {
    const scoreData: ScoreDataByType['Objectives'] = {
      completedByPlayer: [
        { playerId: 'p1', objectives: ['Vittoria'] },
        { playerId: 'p2', objectives: [] },
      ],
    };
    const result = mapScoreDataToPanelData('Objectives', scoreData, PLAYERS, {
      availableObjectives: CATALOGUE,
    });
    expect(result).toEqual({
      kind: 'Objectives',
      players: [
        { id: 'p1', displayName: 'Marco', completedObjectives: ['Vittoria'] },
        { id: 'p2', displayName: 'Anna B.', completedObjectives: [] },
      ],
      objectives: [
        { id: 'Vittoria', label: 'Vittoria', done: true },
        { id: 'Tesoro', label: 'Tesoro', done: false },
      ],
    });
  });
});

// ─── displayName fallback ─────────────────────────────────────────────────────

describe('mapScoreDataToPanelData — displayName fallback', () => {
  it('falls back to name when displayName is undefined', () => {
    const scoreData: ScoreDataByType['Points'] = {
      scores: [{ playerId: 'p1', points: 5 }],
    };
    const players = [{ id: 'p1', name: 'Marco' }]; // no displayName
    const result = mapScoreDataToPanelData('Points', scoreData, players);
    expect(result).toEqual({
      kind: 'Points',
      players: [{ id: 'p1', displayName: 'Marco', score: 5 }],
    });
  });
});

// ─── Missing-player padding per variant ───────────────────────────────────────

describe('mapScoreDataToPanelData — missing-player padding', () => {
  it('pads Points missing player with score=0', () => {
    const scoreData: ScoreDataByType['Points'] = {
      scores: [{ playerId: 'p1', points: 10 }],
    };
    const result = mapScoreDataToPanelData('Points', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'Points',
      players: [
        { id: 'p1', displayName: 'Marco', score: 10 },
        { id: 'p2', displayName: 'Anna B.', score: 0 },
      ],
    });
  });

  it('pads Ranking missing player with position=players.length', () => {
    const scoreData: ScoreDataByType['Ranking'] = {
      positions: [{ playerId: 'p1', position: 1 }],
    };
    const result = mapScoreDataToPanelData('Ranking', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'Ranking',
      players: [
        { id: 'p1', displayName: 'Marco', position: 1 },
        { id: 'p2', displayName: 'Anna B.', position: 2 },
      ],
    });
  });

  it('pads BinaryWin missing player with isWinner=false', () => {
    const scoreData: ScoreDataByType['BinaryWin'] = {
      results: [{ playerId: 'p1', isWinner: true }],
    };
    const result = mapScoreDataToPanelData('BinaryWin', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'BinaryWin',
      players: [
        { id: 'p1', displayName: 'Marco', isWinner: true },
        { id: 'p2', displayName: 'Anna B.', isWinner: false },
      ],
    });
  });

  it('pads Objectives missing player with empty array', () => {
    const scoreData: ScoreDataByType['Objectives'] = {
      completedByPlayer: [{ playerId: 'p1', objectives: ['Vittoria'] }],
    };
    const result = mapScoreDataToPanelData('Objectives', scoreData, PLAYERS, {
      availableObjectives: CATALOGUE,
    });
    expect(result).toEqual({
      kind: 'Objectives',
      players: [
        { id: 'p1', displayName: 'Marco', completedObjectives: ['Vittoria'] },
        { id: 'p2', displayName: 'Anna B.', completedObjectives: [] },
      ],
      objectives: [
        { id: 'Vittoria', label: 'Vittoria', done: true },
        { id: 'Tesoro', label: 'Tesoro', done: false },
      ],
    });
  });
});

// ─── Objectives catalogue edges ───────────────────────────────────────────────

describe('mapScoreDataToPanelData — Objectives catalogue edges', () => {
  it('returns empty objectives array when no availableObjectives passed', () => {
    const scoreData: ScoreDataByType['Objectives'] = {
      completedByPlayer: [{ playerId: 'p1', objectives: ['Vittoria'] }],
    };
    const result = mapScoreDataToPanelData('Objectives', scoreData, PLAYERS);
    expect(result).toMatchObject({
      kind: 'Objectives',
      objectives: [],
    });
  });

  it('marks objective done=true when at least one player completed it', () => {
    const scoreData: ScoreDataByType['Objectives'] = {
      completedByPlayer: [
        { playerId: 'p1', objectives: ['Vittoria'] },
        { playerId: 'p2', objectives: [] },
      ],
    };
    const result = mapScoreDataToPanelData('Objectives', scoreData, PLAYERS, {
      availableObjectives: ['Vittoria', 'Sopravvivenza'],
    });
    expect(result).toMatchObject({
      objectives: [
        { id: 'Vittoria', label: 'Vittoria', done: true },
        { id: 'Sopravvivenza', label: 'Sopravvivenza', done: false },
      ],
    });
  });
});

// ─── Empty players list edge ─────────────────────────────────────────────────

describe('mapScoreDataToPanelData — empty players list', () => {
  it('returns empty players array when players list is empty (Points)', () => {
    const result = mapScoreDataToPanelData('Points', { scores: [] }, []);
    expect(result).toEqual({ kind: 'Points', players: [] });
  });

  it('returns empty players array when players list is empty (Ranking) without invalid position=0', () => {
    const result = mapScoreDataToPanelData('Ranking', { positions: [] }, []);
    expect(result).toEqual({ kind: 'Ranking', players: [] });
  });
});
