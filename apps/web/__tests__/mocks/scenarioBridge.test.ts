import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  clearScenarioBridge,
  getScenarioBridge,
  setScenarioBridge,
  type ScenarioBridge,
} from '@/mocks/scenarioBridge';

describe('scenarioBridge', () => {
  afterEach(() => {
    clearScenarioBridge();
  });

  it('returns null when no bridge is set', () => {
    expect(getScenarioBridge()).toBeNull();
  });

  it('returns the bridge once set', () => {
    const bridge: ScenarioBridge = {
      getGames: () => [],
      getSessions: () => [],
      getChatHistory: () => [],
      getLibrary: () => ({ ownedGameIds: [], wishlistGameIds: [] }),
      getScenarioName: () => 'test',
      addGame: vi.fn(),
      updateGame: vi.fn(),
      removeGame: vi.fn(),
      toggleOwned: vi.fn(),
      toggleWishlist: vi.fn(),
    };
    setScenarioBridge(bridge);
    expect(getScenarioBridge()).toBe(bridge);
  });

  it('replaces the bridge on repeated set (HMR)', () => {
    const first: ScenarioBridge = {
      getGames: () => [{ id: '1', title: 'First' }],
      getSessions: () => [],
      getChatHistory: () => [],
      getLibrary: () => ({ ownedGameIds: [], wishlistGameIds: [] }),
      getScenarioName: () => 'first',
      addGame: vi.fn(),
      updateGame: vi.fn(),
      removeGame: vi.fn(),
      toggleOwned: vi.fn(),
      toggleWishlist: vi.fn(),
    };
    const second: ScenarioBridge = {
      ...first,
      getScenarioName: () => 'second',
    };
    setScenarioBridge(first);
    setScenarioBridge(second);
    expect(getScenarioBridge()?.getScenarioName()).toBe('second');
  });

  it('clearScenarioBridge resets to null', () => {
    const bridge: ScenarioBridge = {
      getGames: () => [],
      getSessions: () => [],
      getChatHistory: () => [],
      getLibrary: () => ({ ownedGameIds: [], wishlistGameIds: [] }),
      getScenarioName: () => 'test',
      addGame: vi.fn(),
      updateGame: vi.fn(),
      removeGame: vi.fn(),
      toggleOwned: vi.fn(),
      toggleWishlist: vi.fn(),
    };
    setScenarioBridge(bridge);
    clearScenarioBridge();
    expect(getScenarioBridge()).toBeNull();
  });
});
