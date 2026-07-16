import { describe, expect, it } from 'vitest';

import { CATAN_PIECE_TOTALS, emptyCatanPlayerState, parseCatanGameState } from '../catan-state';

const VALID = {
  v: 1,
  game: 'catan',
  board: {
    hexes: [{ id: 'h0', col: 0, row: 0, terrain: 'desert', number: null }],
    robberHexId: 'h0',
    ports: [{ hexId: 'h0', edge: 4, type: 'generic', ratio: '3:1' }],
  },
  dice: { last: 8, history: [8, 6] },
  players: {
    p1: {
      handSize: 3,
      built: { settlements: 2, cities: 1, roads: 4 },
      devCount: 2,
      badges: { longestRoad: true, largestArmy: false },
    },
  },
};

describe('parseCatanGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parseCatanGameState(VALID);
    expect(parsed).not.toBeNull();
    expect(parsed?.dice.last).toBe(8);
    expect(parsed?.players.p1?.badges.longestRoad).toBe(true);
  });

  it('returns null for a different game', () => {
    expect(parseCatanGameState({ ...VALID, game: 'wingspan' })).toBeNull();
  });

  it('returns null for a future version', () => {
    expect(parseCatanGameState({ ...VALID, v: 2 })).toBeNull();
  });

  it('returns null for malformed / non-object input', () => {
    expect(parseCatanGameState(null)).toBeNull();
    expect(parseCatanGameState('nope')).toBeNull();
    expect(parseCatanGameState({ v: 1, game: 'catan' })).toBeNull();
  });

  it('accepts optional ports absent', () => {
    const { ports: _drop, ...board } = VALID.board;
    expect(parseCatanGameState({ ...VALID, board })).not.toBeNull();
  });

  it('exposes base-game piece totals', () => {
    expect(CATAN_PIECE_TOTALS).toEqual({ settlements: 5, cities: 4, roads: 15 });
  });

  it('emptyCatanPlayerState is fully zeroed', () => {
    expect(emptyCatanPlayerState()).toEqual({
      handSize: 0,
      built: { settlements: 0, cities: 0, roads: 0 },
      devCount: 0,
      badges: { longestRoad: false, largestArmy: false },
    });
  });
});
