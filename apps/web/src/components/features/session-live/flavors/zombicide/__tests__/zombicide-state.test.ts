import { describe, expect, it } from 'vitest';

import {
  ZOMBIE_TYPES,
  emptyZombieCounts,
  initialZombicideState,
  nextWoundLevel,
  parseZombicideGameState,
} from '../zombicide-state';

const VALID = {
  v: 1,
  game: 'zombicide',
  zombies: { walker: 5, runner: 2, fatty: 0, berserker: 1, abomination: 0, necromancer: 1 },
  survivors: { p1: 0, p2: 2 },
};

describe('parseZombicideGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parseZombicideGameState(VALID);
    expect(parsed?.zombies.walker).toBe(5);
    expect(parsed?.survivors.p2).toBe(2);
  });
  it('returns null for a different game', () => {
    expect(parseZombicideGameState({ ...VALID, game: 'catan' })).toBeNull();
  });
  it('returns null for a future version', () => {
    expect(parseZombicideGameState({ ...VALID, v: 2 })).toBeNull();
  });
  it('returns null when a zombie type is missing', () => {
    expect(
      parseZombicideGameState({
        ...VALID,
        zombies: { walker: 1, runner: 0, fatty: 2, berserker: 0, abomination: 0 },
      })
    ).toBeNull();
  });
  it('returns null when a wound level is 3', () => {
    expect(parseZombicideGameState({ ...VALID, survivors: { p1: 3 } })).toBeNull();
  });
  it('returns null for malformed / non-object', () => {
    expect(parseZombicideGameState(null)).toBeNull();
    expect(parseZombicideGameState('x')).toBeNull();
  });
});

describe('emptyZombieCounts', () => {
  it('is all zero', () => {
    expect(emptyZombieCounts()).toEqual({
      walker: 0,
      runner: 0,
      fatty: 0,
      berserker: 0,
      abomination: 0,
      necromancer: 0,
    });
  });
});

describe('initialZombicideState', () => {
  it('seeds zombies 0 + every player 0 wounds', () => {
    const s = initialZombicideState(['p1', 'p2']);
    expect(s.zombies).toEqual({
      walker: 0,
      runner: 0,
      fatty: 0,
      berserker: 0,
      abomination: 0,
      necromancer: 0,
    });
    expect(s.survivors).toEqual({ p1: 0, p2: 0 });
    expect(s.game).toBe('zombicide');
  });
});

describe('nextWoundLevel', () => {
  it('cycles 0 → 1 → 2 → 0', () => {
    expect(nextWoundLevel(0)).toBe(1);
    expect(nextWoundLevel(1)).toBe(2);
    expect(nextWoundLevel(2)).toBe(0);
  });
});

describe('constants', () => {
  it('lists the 6 zombie types in order', () => {
    expect(ZOMBIE_TYPES).toEqual([
      'walker',
      'runner',
      'fatty',
      'berserker',
      'abomination',
      'necromancer',
    ]);
  });
});
