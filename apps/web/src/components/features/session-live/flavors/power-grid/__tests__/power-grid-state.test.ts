import { describe, expect, it } from 'vitest';

import {
  POWER_GRID_PLANT_BANKS,
  POWER_GRID_RESOURCES,
  emptyPowerGridResources,
  initialPowerGridState,
  parsePowerGridGameState,
} from '../power-grid-state';

const VALID = {
  v: 1,
  game: 'power-grid',
  plants: { current: [3, 4, null, 6], future: [null, null, null, null] },
  resources: { coal: 5, oil: 2, garbage: 0, uranium: 1 },
};

describe('parsePowerGridGameState', () => {
  it('parses a well-formed state', () => {
    const parsed = parsePowerGridGameState(VALID);
    expect(parsed?.plants.current[0]).toBe(3);
    expect(parsed?.plants.current[2]).toBeNull();
    expect(parsed?.resources.coal).toBe(5);
  });
  it('returns null for a different game', () => {
    expect(parsePowerGridGameState({ ...VALID, game: 'catan' })).toBeNull();
  });
  it('returns null for a future version', () => {
    expect(parsePowerGridGameState({ ...VALID, v: 2 })).toBeNull();
  });
  it('returns null when a plant bank is not length 4', () => {
    expect(
      parsePowerGridGameState({
        ...VALID,
        plants: { current: [1, 2], future: [null, null, null, null] },
      })
    ).toBeNull();
  });
  it('returns null when a resource is missing', () => {
    expect(
      parsePowerGridGameState({ ...VALID, resources: { coal: 1, oil: 0, garbage: 2 } })
    ).toBeNull();
  });
  it('returns null for malformed / non-object', () => {
    expect(parsePowerGridGameState(null)).toBeNull();
    expect(parsePowerGridGameState('x')).toBeNull();
  });
});

describe('emptyPowerGridResources', () => {
  it('is all zero', () => {
    expect(emptyPowerGridResources()).toEqual({ coal: 0, oil: 0, garbage: 0, uranium: 0 });
  });
});

describe('initialPowerGridState', () => {
  it('seeds 4 null slots per bank + 0 resources', () => {
    const s = initialPowerGridState();
    expect(s.plants.current).toEqual([null, null, null, null]);
    expect(s.plants.future).toEqual([null, null, null, null]);
    expect(s.resources).toEqual({ coal: 0, oil: 0, garbage: 0, uranium: 0 });
    expect(s.game).toBe('power-grid');
  });
});

describe('constants', () => {
  it('lists the 4 resources and 2 banks in order', () => {
    expect(POWER_GRID_RESOURCES).toEqual(['coal', 'oil', 'garbage', 'uranium']);
    expect(POWER_GRID_PLANT_BANKS).toEqual(['current', 'future']);
  });
});
