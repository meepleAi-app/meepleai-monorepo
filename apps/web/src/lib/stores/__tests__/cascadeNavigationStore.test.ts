/**
 * Cascade Navigation Store Tests
 *
 * Tests the Mana Pip -> DeckStack -> Drawer cascade navigation flow.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useCascadeNavigationStore } from '../cascadeNavigationStore';

describe('useCascadeNavigationStore', () => {
  beforeEach(() => {
    const { result } = renderHook(() => useCascadeNavigationStore());
    act(() => {
      result.current.closeCascade();
    });
  });

  it('starts in closed state with null values', () => {
    const { result } = renderHook(() => useCascadeNavigationStore());

    expect(result.current.state).toBe('closed');
    expect(result.current.activeEntityType).toBeNull();
    expect(result.current.activeEntityId).toBeNull();
    expect(result.current.sourceEntityId).toBeNull();
    expect(result.current.anchorRect).toBeNull();
    expect(result.current.deckStackSkipped).toBe(false);
  });

  it('openDeckStack sets correct state', () => {
    const { result } = renderHook(() => useCascadeNavigationStore());
    const mockRect = new DOMRect(10, 20, 100, 50);

    act(() => {
      result.current.openDeckStack('game', 'source-123', mockRect);
    });

    expect(result.current.state).toBe('deckStack');
    expect(result.current.activeEntityType).toBe('game');
    expect(result.current.sourceEntityId).toBe('source-123');
    expect(result.current.anchorRect).toBe(mockRect);
    expect(result.current.deckStackSkipped).toBe(false);
  });

  it('openDeckStack works without anchor rect', () => {
    const { result } = renderHook(() => useCascadeNavigationStore());

    act(() => {
      result.current.openDeckStack('player', 'source-456');
    });

    expect(result.current.state).toBe('deckStack');
    expect(result.current.activeEntityType).toBe('player');
    expect(result.current.sourceEntityId).toBe('source-456');
    expect(result.current.anchorRect).toBeNull();
  });

  it('openDrawer from deckStack sets correct state (deckStackSkipped=false)', () => {
    const { result } = renderHook(() => useCascadeNavigationStore());

    // First open deck stack
    act(() => {
      result.current.openDeckStack('game', 'source-123');
    });

    // Then open drawer from deck stack
    act(() => {
      result.current.openDrawer('game', 'entity-789');
    });

    expect(result.current.state).toBe('drawer');
    expect(result.current.activeEntityType).toBe('game');
    expect(result.current.activeEntityId).toBe('entity-789');
    expect(result.current.deckStackSkipped).toBe(false);
  });

  it('closeDrawer returns to deckStack when not skipped', () => {
    const { result } = renderHook(() => useCascadeNavigationStore());

    // Open deck stack, then drawer
    act(() => {
      result.current.openDeckStack('game', 'source-123');
    });
    act(() => {
      result.current.openDrawer('game', 'entity-789');
    });

    // Close drawer should return to deckStack
    act(() => {
      result.current.closeDrawer();
    });

    expect(result.current.state).toBe('deckStack');
    expect(result.current.activeEntityId).toBeNull();
  });

  it('openDrawer directly (without deckStack) marks deckStackSkipped=true', () => {
    const { result } = renderHook(() => useCascadeNavigationStore());

    // Open drawer directly without going through deck stack
    act(() => {
      result.current.openDrawer('collection', 'entity-abc');
    });

    expect(result.current.state).toBe('drawer');
    expect(result.current.activeEntityType).toBe('collection');
    expect(result.current.activeEntityId).toBe('entity-abc');
    expect(result.current.deckStackSkipped).toBe(true);
  });

  it('closeDrawer returns to closed when skipped', () => {
    const { result } = renderHook(() => useCascadeNavigationStore());

    // Open drawer directly (skipping deck stack)
    act(() => {
      result.current.openDrawer('collection', 'entity-abc');
    });

    // Close drawer should return to closed (not deckStack)
    act(() => {
      result.current.closeDrawer();
    });

    expect(result.current.state).toBe('closed');
    expect(result.current.activeEntityType).toBeNull();
    expect(result.current.activeEntityId).toBeNull();
  });

  it('closeCascade resets everything', () => {
    const { result } = renderHook(() => useCascadeNavigationStore());
    const mockRect = new DOMRect(10, 20, 100, 50);

    // Set up some state
    act(() => {
      result.current.openDeckStack('game', 'source-123', mockRect);
    });
    act(() => {
      result.current.openDrawer('game', 'entity-789');
    });

    // Close cascade should reset everything
    act(() => {
      result.current.closeCascade();
    });

    expect(result.current.state).toBe('closed');
    expect(result.current.activeEntityType).toBeNull();
    expect(result.current.activeEntityId).toBeNull();
    expect(result.current.sourceEntityId).toBeNull();
    expect(result.current.anchorRect).toBeNull();
    expect(result.current.deckStackSkipped).toBe(false);
  });
});
