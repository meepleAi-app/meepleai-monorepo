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

  // Task 5 — mode discriminator tests
  it('defaults to idle mode', () => {
    expect(useQuickViewStore.getState().mode).toBe('idle');
  });

  it('defaults sessionId to null', () => {
    expect(useQuickViewStore.getState().sessionId).toBeNull();
  });

  it('sets game mode when opening for game', () => {
    useQuickViewStore.getState().openForGame('game-123');
    const state = useQuickViewStore.getState();
    expect(state.mode).toBe('game');
    expect(state.selectedGameId).toBe('game-123');
    expect(state.isOpen).toBe(true);
    expect(state.isCollapsed).toBe(false);
  });

  it('sets session mode when opening for session', () => {
    useQuickViewStore.getState().openForSession('session-456', 'game-789');
    const state = useQuickViewStore.getState();
    expect(state.mode).toBe('session');
    expect(state.sessionId).toBe('session-456');
    expect(state.selectedGameId).toBe('game-789');
    expect(state.isOpen).toBe(true);
    expect(state.isCollapsed).toBe(false);
  });

  it('resets to idle on close', () => {
    useQuickViewStore.getState().openForGame('game-123');
    useQuickViewStore.getState().close();
    const state = useQuickViewStore.getState();
    expect(state.mode).toBe('idle');
    expect(state.sessionId).toBeNull();
    expect(state.selectedGameId).toBeNull();
  });

  it('resets session state to idle on close after session mode', () => {
    useQuickViewStore.getState().openForSession('session-456', 'game-789');
    useQuickViewStore.getState().close();
    const state = useQuickViewStore.getState();
    expect(state.mode).toBe('idle');
    expect(state.sessionId).toBeNull();
    expect(state.selectedGameId).toBeNull();
  });
});
