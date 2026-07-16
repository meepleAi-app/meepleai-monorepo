import { describe, expect, it } from 'vitest';

import {
  WINGSPAN_CATEGORIES,
  WINGSPAN_ROUND_TURN_BUDGET,
  initialWingspanState,
  parseWingspanGameState,
} from '../wingspan-state';

const VALID = {
  v: 1,
  game: 'wingspan',
  round: 3,
  roundGoals: [{ label: 'Nidi' }, { label: 'Uova nel forest' }],
};

describe('parseWingspanGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parseWingspanGameState(VALID);
    expect(parsed?.round).toBe(3);
    expect(parsed?.roundGoals).toHaveLength(2);
  });

  it('returns null for a different game', () => {
    expect(parseWingspanGameState({ ...VALID, game: 'catan' })).toBeNull();
  });

  it('returns null for a future version', () => {
    expect(parseWingspanGameState({ ...VALID, v: 2 })).toBeNull();
  });

  it('returns null for a round out of range', () => {
    expect(parseWingspanGameState({ ...VALID, round: 0 })).toBeNull();
    expect(parseWingspanGameState({ ...VALID, round: 5 })).toBeNull();
  });

  it('returns null for malformed / non-object input', () => {
    expect(parseWingspanGameState(null)).toBeNull();
    expect(parseWingspanGameState('nope')).toBeNull();
    expect(parseWingspanGameState({ v: 1, game: 'wingspan' })).toBeNull();
  });

  it('accepts empty roundGoals', () => {
    expect(parseWingspanGameState({ ...VALID, roundGoals: [] })?.roundGoals).toEqual([]);
  });
});

describe('initialWingspanState', () => {
  it('starts at round 1 with no goals', () => {
    expect(initialWingspanState()).toEqual({ v: 1, game: 'wingspan', round: 1, roundGoals: [] });
  });
});

describe('constants', () => {
  it('has the standard 4-round turn budget', () => {
    expect(WINGSPAN_ROUND_TURN_BUDGET).toEqual([8, 7, 6, 5]);
  });

  it('exposes the 6 canonical VP category ids', () => {
    expect(WINGSPAN_CATEGORIES.map(c => c.id)).toEqual([
      'birds',
      'bonusCards',
      'endOfRoundGoals',
      'eggs',
      'cachedFood',
      'tuckedCards',
    ]);
  });
});
