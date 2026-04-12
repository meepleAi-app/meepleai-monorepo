import { describe, it, expect } from 'vitest';
import { createScenarioStore } from '@/dev-tools/scenarioStore';
import type { Scenario } from '@/dev-tools/types';

const BASE: Scenario = {
  name: 'test',
  description: 'Test',
  auth: {
    currentUser: { id: 'MOCK-u1', email: 'u@m.local', displayName: 'U', role: 'User' },
    availableUsers: [],
  },
  games: [
    { id: 'MOCK-g1', title: 'Wingspan' },
    { id: 'MOCK-g2', title: 'Scythe' },
  ],
  sessions: [],
  library: { ownedGameIds: ['MOCK-g1'], wishlistGameIds: [] },
  chatHistory: [],
};

describe('scenarioStore', () => {
  it('loads a scenario and exposes its data', () => {
    const store = createScenarioStore(BASE);
    const state = store.getState();
    expect(state.scenario.name).toBe('test');
    expect(state.games).toHaveLength(2);
  });

  it('addGame appends to games', () => {
    const store = createScenarioStore(BASE);
    store.getState().addGame({ id: 'MOCK-g3', title: 'Terraforming Mars' });
    expect(store.getState().games).toHaveLength(3);
    expect(store.getState().games[2].title).toBe('Terraforming Mars');
  });

  it('removeGame deletes by id', () => {
    const store = createScenarioStore(BASE);
    store.getState().removeGame('MOCK-g1');
    expect(store.getState().games).toHaveLength(1);
    expect(store.getState().games[0].id).toBe('MOCK-g2');
  });

  it('updateGame patches fields by id', () => {
    const store = createScenarioStore(BASE);
    store.getState().updateGame('MOCK-g1', { averageRating: 9.5 });
    expect(store.getState().games[0].averageRating).toBe(9.5);
    expect(store.getState().games[0].title).toBe('Wingspan');
  });

  it('resetToScenario restores initial state', () => {
    const store = createScenarioStore(BASE);
    store.getState().addGame({ id: 'MOCK-new', title: 'New' });
    expect(store.getState().games).toHaveLength(3);
    store.getState().resetToScenario();
    expect(store.getState().games).toHaveLength(2);
    expect(store.getState().games.find(g => g.id === 'MOCK-new')).toBeUndefined();
  });

  it('loadScenario replaces the entire scenario', () => {
    const store = createScenarioStore(BASE);
    const next: Scenario = { ...BASE, name: 'next', games: [{ id: 'MOCK-x', title: 'X' }] };
    store.getState().loadScenario(next);
    expect(store.getState().scenario.name).toBe('next');
    expect(store.getState().games).toHaveLength(1);
  });

  it('isSwitching flag controls 503-guard during load', () => {
    const store = createScenarioStore(BASE);
    store.getState().beginSwitch();
    expect(store.getState().isSwitching).toBe(true);
    store.getState().endSwitch();
    expect(store.getState().isSwitching).toBe(false);
  });
});
