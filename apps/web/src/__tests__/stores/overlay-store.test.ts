import { describe, it, expect, beforeEach } from 'vitest';
import { useOverlayStore } from '@/lib/stores/overlay-store';

describe('useOverlayStore', () => {
  beforeEach(() => {
    useOverlayStore.setState({
      isOpen: false,
      entityType: null,
      entityId: null,
      deckItems: null,
      deckIndex: 0,
    });
  });

  it('starts closed with no entity', () => {
    const state = useOverlayStore.getState();
    expect(state.isOpen).toBe(false);
    expect(state.entityType).toBeNull();
    expect(state.entityId).toBeNull();
  });

  it('open sets entity and opens', () => {
    useOverlayStore.getState().open('player', 'mario-id');
    const state = useOverlayStore.getState();
    expect(state.isOpen).toBe(true);
    expect(state.entityType).toBe('player');
    expect(state.entityId).toBe('mario-id');
  });

  it('close resets state', () => {
    useOverlayStore.getState().open('game', 'catan-id');
    useOverlayStore.getState().close();
    const state = useOverlayStore.getState();
    expect(state.isOpen).toBe(false);
    expect(state.entityType).toBeNull();
  });

  it('replace swaps entity without closing', () => {
    useOverlayStore.getState().open('player', 'mario-id');
    useOverlayStore.getState().open('game', 'catan-id');
    const state = useOverlayStore.getState();
    expect(state.isOpen).toBe(true);
    expect(state.entityType).toBe('game');
    expect(state.entityId).toBe('catan-id');
  });

  it('serializes to URL param format', () => {
    useOverlayStore.getState().open('player', 'mario-id');
    expect(useOverlayStore.getState().toUrlParam()).toBe('player:mario-id');
  });

  it('deserializes from URL param format', () => {
    useOverlayStore.getState().fromUrlParam('game:catan-id');
    const state = useOverlayStore.getState();
    expect(state.isOpen).toBe(true);
    expect(state.entityType).toBe('game');
    expect(state.entityId).toBe('catan-id');
  });

  it('fromUrlParam with null closes', () => {
    useOverlayStore.getState().open('game', 'x');
    useOverlayStore.getState().fromUrlParam(null);
    expect(useOverlayStore.getState().isOpen).toBe(false);
  });

  it('openDeck sets items and active index', () => {
    const items = [
      { entityType: 'player', entityId: 'a' },
      { entityType: 'player', entityId: 'b' },
      { entityType: 'player', entityId: 'c' },
    ];
    useOverlayStore.getState().openDeck(items, 1);
    const state = useOverlayStore.getState();
    expect(state.isOpen).toBe(true);
    expect(state.deckItems).toHaveLength(3);
    expect(state.deckIndex).toBe(1);
    expect(state.entityId).toBe('b');
  });

  it('setDeckIndex navigates within deck', () => {
    const items = [
      { entityType: 'player', entityId: 'a' },
      { entityType: 'player', entityId: 'b' },
    ];
    useOverlayStore.getState().openDeck(items, 0);
    useOverlayStore.getState().setDeckIndex(1);
    expect(useOverlayStore.getState().entityId).toBe('b');
    expect(useOverlayStore.getState().deckIndex).toBe(1);
  });

  it('setDeckIndex ignores out of bounds', () => {
    const items = [{ entityType: 'player', entityId: 'a' }];
    useOverlayStore.getState().openDeck(items, 0);
    useOverlayStore.getState().setDeckIndex(5);
    expect(useOverlayStore.getState().deckIndex).toBe(0);
  });
});
