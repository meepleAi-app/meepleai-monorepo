import { act } from 'react';
import { describe, it, expect, beforeEach } from 'vitest';
import { useCascadeNavigationStore } from '../cascade-navigation-store';

describe('drawer stack', () => {
  beforeEach(() => {
    act(() => useCascadeNavigationStore.getState().closeCascade());
  });

  it('pushDrawer pushes current onto stack and opens new', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('game', 'g1'));
    act(() => store.getState().pushDrawer('agent', 'a1'));

    const state = store.getState();
    expect(state.state).toBe('drawer');
    expect(state.activeEntityType).toBe('agent');
    expect(state.activeEntityId).toBe('a1');
    expect(state.drawerStack).toHaveLength(1);
    expect(state.drawerStack[0].entityType).toBe('game');
    expect(state.drawerStack[0].entityId).toBe('g1');
  });

  it('popDrawer restores previous drawer from stack', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('game', 'g1'));
    act(() => store.getState().pushDrawer('agent', 'a1'));
    act(() => store.getState().popDrawer());

    const state = store.getState();
    expect(state.state).toBe('drawer');
    expect(state.activeEntityType).toBe('game');
    expect(state.activeEntityId).toBe('g1');
    expect(state.drawerStack).toHaveLength(0);
  });

  it('popDrawer closes drawer when stack is empty', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('game', 'g1'));
    act(() => store.getState().popDrawer());

    expect(store.getState().state).toBe('closed');
    expect(store.getState().drawerStack).toHaveLength(0);
  });

  it('popDrawer returns to deckStack when opened from deckStack and stack is empty', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDeckStack('game', 'src1'));
    act(() => store.getState().openDrawer('game', 'g1'));
    act(() => store.getState().popDrawer());

    expect(store.getState().state).toBe('deckStack');
  });

  it('drawer stack max depth is 3', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('game', 'g1'));
    act(() => store.getState().pushDrawer('agent', 'a1'));
    act(() => store.getState().pushDrawer('kb', 'k1'));
    act(() => store.getState().pushDrawer('chat', 'c1'));
    act(() => store.getState().pushDrawer('session', 's1')); // exceeds 3

    const state = store.getState();
    expect(state.drawerStack).toHaveLength(3);
    // oldest (game g1) was evicted
    expect(state.drawerStack[0].entityType).toBe('agent');
  });

  it('openDrawer accepts optional tabId', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('session', 's1', 'toolkit'));

    expect(store.getState().activeTabId).toBe('toolkit');
  });

  it('closeCascade clears the stack', () => {
    const store = useCascadeNavigationStore;
    act(() => store.getState().openDrawer('game', 'g1'));
    act(() => store.getState().pushDrawer('agent', 'a1'));
    act(() => store.getState().closeCascade());

    expect(store.getState().drawerStack).toHaveLength(0);
    expect(store.getState().state).toBe('closed');
  });
});
