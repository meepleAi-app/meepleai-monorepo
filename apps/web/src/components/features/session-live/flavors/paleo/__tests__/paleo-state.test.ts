import { describe, expect, it } from 'vitest';

import {
  PALEO_RESOURCES,
  PALEO_STATUSES,
  emptyPaleoResources,
  initialPaleoState,
  nextPaleoStatus,
  parsePaleoGameState,
} from '../paleo-state';

const VALID = {
  v: 1,
  game: 'paleo',
  resources: { wood: 2, stone: 0, food: 1, knowledge: 3 },
  survivors: { p1: 'alive', p2: 'wounded' },
};

describe('parsePaleoGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parsePaleoGameState(VALID);
    expect(parsed?.resources.knowledge).toBe(3);
    expect(parsed?.survivors.p2).toBe('wounded');
  });
  it('returns null for a different game', () => {
    expect(parsePaleoGameState({ ...VALID, game: 'catan' })).toBeNull();
  });
  it('returns null for a future version', () => {
    expect(parsePaleoGameState({ ...VALID, v: 2 })).toBeNull();
  });
  it('returns null when a resource is missing', () => {
    expect(parsePaleoGameState({ ...VALID, resources: { wood: 1, stone: 0, food: 2 } })).toBeNull();
  });
  it('returns null for an invalid status', () => {
    expect(parsePaleoGameState({ ...VALID, survivors: { p1: 'zombie' } })).toBeNull();
  });
  it('returns null for malformed / non-object', () => {
    expect(parsePaleoGameState(null)).toBeNull();
    expect(parsePaleoGameState('x')).toBeNull();
  });
});

describe('emptyPaleoResources', () => {
  it('is all zero', () => {
    expect(emptyPaleoResources()).toEqual({ wood: 0, stone: 0, food: 0, knowledge: 0 });
  });
});

describe('initialPaleoState', () => {
  it('seeds resources 0 + every player alive', () => {
    const s = initialPaleoState(['p1', 'p2']);
    expect(s.resources).toEqual({ wood: 0, stone: 0, food: 0, knowledge: 0 });
    expect(s.survivors).toEqual({ p1: 'alive', p2: 'alive' });
    expect(s.game).toBe('paleo');
  });
});

describe('nextPaleoStatus', () => {
  it('cycles alive → wounded → dead → alive', () => {
    expect(nextPaleoStatus('alive')).toBe('wounded');
    expect(nextPaleoStatus('wounded')).toBe('dead');
    expect(nextPaleoStatus('dead')).toBe('alive');
  });
});

describe('constants', () => {
  it('lists the 4 resources and 3 statuses in order', () => {
    expect(PALEO_RESOURCES).toEqual(['wood', 'stone', 'food', 'knowledge']);
    expect(PALEO_STATUSES).toEqual(['alive', 'wounded', 'dead']);
  });
});
