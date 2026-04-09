import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useMyHandStore } from '../store';
import type { MyHandSlotType } from '../types';

describe('useMyHandStore', () => {
  beforeEach(() => {
    useMyHandStore.setState(useMyHandStore.getInitialState());
  });

  it('starts with all 4 slots empty', () => {
    const state = useMyHandStore.getState();
    expect(state.slots.toolkit.entityId).toBeNull();
    expect(state.slots.game.entityId).toBeNull();
    expect(state.slots.session.entityId).toBeNull();
    expect(state.slots.ai.entityId).toBeNull();
  });

  it('assigns entity to a slot', () => {
    useMyHandStore.getState().assignSlot('toolkit', {
      entityId: 'tk-1',
      entityType: 'toolkit',
      entityLabel: 'My Toolkit',
      entityImageUrl: null,
    });
    const slot = useMyHandStore.getState().slots.toolkit;
    expect(slot.entityId).toBe('tk-1');
    expect(slot.entityType).toBe('toolkit');
    expect(slot.entityLabel).toBe('My Toolkit');
    expect(slot.pinnedAt).not.toBeNull();
  });

  it('clears a slot', () => {
    useMyHandStore.getState().assignSlot('game', {
      entityId: 'g-1',
      entityType: 'game',
      entityLabel: 'Agricola',
      entityImageUrl: null,
    });
    useMyHandStore.getState().clearSlot('game');
    const slot = useMyHandStore.getState().slots.game;
    expect(slot.entityId).toBeNull();
    expect(slot.entityLabel).toBeNull();
    expect(slot.pinnedAt).toBeNull();
  });

  it('marks slot as invalid', () => {
    useMyHandStore.getState().assignSlot('session', {
      entityId: 's-1',
      entityType: 'session',
      entityLabel: 'Friday Game',
      entityImageUrl: null,
    });
    useMyHandStore.getState().markSlotInvalid('session');
    expect(useMyHandStore.getState().slots.session.isEntityValid).toBe(false);
  });

  it('toggles sidebar collapsed state', () => {
    expect(useMyHandStore.getState().isSidebarCollapsed).toBe(false);
    useMyHandStore.getState().toggleSidebarCollapsed();
    expect(useMyHandStore.getState().isSidebarCollapsed).toBe(true);
    useMyHandStore.getState().toggleSidebarCollapsed();
    expect(useMyHandStore.getState().isSidebarCollapsed).toBe(false);
  });

  it('toggles mobile expanded state', () => {
    expect(useMyHandStore.getState().isMobileExpanded).toBe(false);
    useMyHandStore.getState().toggleMobileExpanded();
    expect(useMyHandStore.getState().isMobileExpanded).toBe(true);
  });
});
