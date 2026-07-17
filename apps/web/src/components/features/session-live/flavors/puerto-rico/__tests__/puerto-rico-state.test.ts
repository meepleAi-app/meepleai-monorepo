import { describe, expect, it } from 'vitest';

import {
  PUERTO_RICO_GOODS,
  emptyPuertoRicoPlayerState,
  initialPuertoRicoState,
  parsePuertoRicoGameState,
} from '../puerto-rico-state';

const PLAYER = {
  doubloons: 3,
  colonists: 2,
  storehouse: { corn: 1, indigo: 0, sugar: 2, tobacco: 0, coffee: 1 },
  plantations: 4,
  quarries: 1,
  buildings: 3,
};
const VALID = {
  v: 1,
  game: 'puerto-rico',
  players: { p1: PLAYER },
  galleons: [
    { good: 'corn', loaded: 2, cap: 5 },
    { good: null, loaded: 0, cap: 6 },
    { good: null, loaded: 0, cap: 7 },
  ],
  tradingHouse: { slots: ['indigo', null, null, null] },
  colonistShip: { onShip: 3, supply: 20 },
};

describe('parsePuertoRicoGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parsePuertoRicoGameState(VALID);
    expect(parsed?.players.p1?.doubloons).toBe(3);
    expect(parsed?.galleons).toHaveLength(3);
  });
  it('returns null for a different game', () => {
    expect(parsePuertoRicoGameState({ ...VALID, game: 'catan' })).toBeNull();
  });
  it('returns null for a future version', () => {
    expect(parsePuertoRicoGameState({ ...VALID, v: 2 })).toBeNull();
  });
  it('returns null when tradingHouse has != 4 slots', () => {
    expect(
      parsePuertoRicoGameState({ ...VALID, tradingHouse: { slots: ['corn', null] } })
    ).toBeNull();
  });
  it('returns null when a storehouse good is missing', () => {
    const bad = { ...PLAYER, storehouse: { corn: 1, indigo: 0, sugar: 2, tobacco: 0 } };
    expect(parsePuertoRicoGameState({ ...VALID, players: { p1: bad } })).toBeNull();
  });
  it('returns null for malformed / non-object', () => {
    expect(parsePuertoRicoGameState(null)).toBeNull();
    expect(parsePuertoRicoGameState('x')).toBeNull();
  });
});

describe('emptyPuertoRicoPlayerState', () => {
  it('is fully zeroed with all 5 goods', () => {
    expect(emptyPuertoRicoPlayerState()).toEqual({
      doubloons: 0,
      colonists: 0,
      storehouse: { corn: 0, indigo: 0, sugar: 0, tobacco: 0, coffee: 0 },
      plantations: 0,
      quarries: 0,
      buildings: 0,
    });
  });
});

describe('initialPuertoRicoState', () => {
  it('seeds a zeroed player per id + galleon caps [n+1, n+2, n+3]', () => {
    const s = initialPuertoRicoState(['p1', 'p2', 'p3']); // n = 3
    expect(Object.keys(s.players)).toEqual(['p1', 'p2', 'p3']);
    expect(s.players.p1?.doubloons).toBe(0);
    expect(s.galleons.map(g => g.cap)).toEqual([4, 5, 6]);
    expect(s.tradingHouse.slots).toEqual([null, null, null, null]);
    expect(s.colonistShip).toEqual({ onShip: 0, supply: 0 });
  });
  it('scales galleon caps with player count', () => {
    expect(initialPuertoRicoState(['a', 'b', 'c', 'd', 'e']).galleons.map(g => g.cap)).toEqual([
      6, 7, 8,
    ]);
  });
});

describe('constants', () => {
  it('has the 5 canonical goods in order', () => {
    expect(PUERTO_RICO_GOODS).toEqual(['corn', 'indigo', 'sugar', 'tobacco', 'coffee']);
  });
});
