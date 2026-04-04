import { describe, it, expect, beforeEach } from 'vitest';
import { useDashboardSearchStore } from '@/stores/useDashboardSearchStore';

describe('useDashboardSearchStore', () => {
  beforeEach(() => {
    useDashboardSearchStore.getState().reset();
  });

  it('starts with search closed', () => {
    const state = useDashboardSearchStore.getState();
    expect(state.isSearchOpen).toBe(false);
    expect(state.selectedGame).toBeNull();
    expect(state.drawerState).toBeNull();
  });

  it('opens and closes search', () => {
    const { openSearch, closeSearch } = useDashboardSearchStore.getState();
    openSearch();
    expect(useDashboardSearchStore.getState().isSearchOpen).toBe(true);
    closeSearch();
    expect(useDashboardSearchStore.getState().isSearchOpen).toBe(false);
  });

  it('sets selected game for modal', () => {
    const game = { id: 'g1', name: 'Catan', imageUrl: null, playerCount: '3-4' };
    useDashboardSearchStore.getState().setSelectedGame(game);
    expect(useDashboardSearchStore.getState().selectedGame).toEqual(game);
  });

  it('sets drawer state for live chat', () => {
    const drawer = { threadId: 't1', agentId: 'a1', gameId: 'g1', gameName: 'Catan' };
    useDashboardSearchStore.getState().openChatDrawer(drawer);
    expect(useDashboardSearchStore.getState().drawerState).toEqual(drawer);
  });

  it('clears drawer state on close', () => {
    const drawer = { threadId: 't1', agentId: 'a1', gameId: 'g1', gameName: 'Catan' };
    useDashboardSearchStore.getState().openChatDrawer(drawer);
    useDashboardSearchStore.getState().closeChatDrawer();
    expect(useDashboardSearchStore.getState().drawerState).toBeNull();
  });

  it('reset clears everything', () => {
    useDashboardSearchStore.getState().openSearch();
    useDashboardSearchStore
      .getState()
      .setSelectedGame({ id: 'g1', name: 'X', imageUrl: null, playerCount: '2' });
    useDashboardSearchStore.getState().reset();
    const state = useDashboardSearchStore.getState();
    expect(state.isSearchOpen).toBe(false);
    expect(state.selectedGame).toBeNull();
    expect(state.drawerState).toBeNull();
  });
});
