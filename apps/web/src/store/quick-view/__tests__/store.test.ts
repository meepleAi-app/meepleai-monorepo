import { describe, expect, it, beforeEach } from 'vitest';
import { useQuickViewStore } from '../store';

describe('useQuickViewStore', () => {
  beforeEach(() => {
    useQuickViewStore.setState(useQuickViewStore.getInitialState());
  });

  it('starts closed with no game selected', () => {
    const state = useQuickViewStore.getState();
    expect(state.isOpen).toBe(false);
    expect(state.selectedGameId).toBeNull();
    expect(state.activeTab).toBe('rules');
  });

  it('opens with a game id', () => {
    useQuickViewStore.getState().openForGame('game-123');
    const state = useQuickViewStore.getState();
    expect(state.isOpen).toBe(true);
    expect(state.selectedGameId).toBe('game-123');
  });

  it('closes and clears selection', () => {
    useQuickViewStore.getState().openForGame('game-123');
    useQuickViewStore.getState().close();
    const state = useQuickViewStore.getState();
    expect(state.isOpen).toBe(false);
    expect(state.selectedGameId).toBeNull();
  });

  it('switches active tab', () => {
    useQuickViewStore.getState().setActiveTab('faq');
    expect(useQuickViewStore.getState().activeTab).toBe('faq');
  });

  it('toggles collapsed state', () => {
    useQuickViewStore.getState().toggleCollapsed();
    expect(useQuickViewStore.getState().isCollapsed).toBe(true);
    useQuickViewStore.getState().toggleCollapsed();
    expect(useQuickViewStore.getState().isCollapsed).toBe(false);
  });
});
