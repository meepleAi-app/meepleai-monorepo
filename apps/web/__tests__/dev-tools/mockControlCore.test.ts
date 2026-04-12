import { describe, it, expect } from 'vitest';
import {
  createMockControlStore,
  parseGroupList,
  computeGroupToggles,
} from '@/dev-tools/mockControlCore';

describe('parseGroupList', () => {
  it('splits comma-separated list', () => {
    expect(parseGroupList('auth,games,chat')).toEqual(['auth', 'games', 'chat']);
  });
  it('trims whitespace', () => {
    expect(parseGroupList(' auth , games ')).toEqual(['auth', 'games']);
  });
  it('returns empty for null/empty', () => {
    expect(parseGroupList(null)).toEqual([]);
    expect(parseGroupList('')).toEqual([]);
  });
});

describe('computeGroupToggles', () => {
  const allGroups = ['auth', 'games', 'chat', 'library', 'admin'];

  it('empty enable + empty disable = all enabled', () => {
    expect(computeGroupToggles(allGroups, [], [])).toEqual({
      auth: true,
      games: true,
      chat: true,
      library: true,
      admin: true,
    });
  });

  it('enable list restricts to only listed', () => {
    expect(computeGroupToggles(allGroups, ['auth', 'games'], [])).toEqual({
      auth: true,
      games: true,
      chat: false,
      library: false,
      admin: false,
    });
  });

  it('disable has precedence over enable', () => {
    expect(computeGroupToggles(allGroups, ['auth', 'games'], ['games'])).toEqual({
      auth: true,
      games: false,
      chat: false,
      library: false,
      admin: false,
    });
  });

  it('disable without enable disables only those', () => {
    expect(computeGroupToggles(allGroups, [], ['admin'])).toEqual({
      auth: true,
      games: true,
      chat: true,
      library: true,
      admin: false,
    });
  });
});

describe('mockControlStore', () => {
  it('initializes with computed group toggles', () => {
    const store = createMockControlStore({
      allGroups: ['auth', 'games'],
      enableList: ['auth'],
      disableList: [],
    });
    expect(store.getState().toggles.groups).toEqual({ auth: true, games: false });
  });

  it('setGroup updates a single group', () => {
    const store = createMockControlStore({
      allGroups: ['auth', 'games'],
      enableList: [],
      disableList: [],
    });
    store.getState().setGroup('games', false);
    expect(store.getState().toggles.groups.games).toBe(false);
    expect(store.getState().toggles.groups.auth).toBe(true);
  });

  it('setEndpointOverride stores per-endpoint override', () => {
    const store = createMockControlStore({
      allGroups: ['games'],
      enableList: [],
      disableList: [],
    });
    store.getState().setEndpointOverride('games.POST /api/v1/games', false);
    expect(store.getState().toggles.overrides['games.POST /api/v1/games']).toBe(false);
  });
});
