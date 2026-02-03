/**
 * UI Slice Tests
 * Issue #3238: Agent UI state management
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { create } from 'zustand';

import { createUISlice, UISlice } from '../uiSlice';

// Create a test store with only the UI slice
function createTestStore() {
  return create<UISlice>()((...args) => ({
    ...createUISlice(...args),
  }));
}

describe('uiSlice', () => {
  let store: ReturnType<typeof createTestStore>;

  beforeEach(() => {
    store = createTestStore();
  });

  describe('Initial State', () => {
    it('has isConfigOpen set to false initially', () => {
      expect(store.getState().isConfigOpen).toBe(false);
    });

    it('has isChatOpen set to false initially', () => {
      expect(store.getState().isChatOpen).toBe(false);
    });

    it('has selectedGameId set to null initially', () => {
      expect(store.getState().selectedGameId).toBeNull();
    });

    it('has selectedTypologyId set to null initially', () => {
      expect(store.getState().selectedTypologyId).toBeNull();
    });

    it('has selectedModelId set to null initially', () => {
      expect(store.getState().selectedModelId).toBeNull();
    });
  });

  describe('Config Actions', () => {
    it('openConfig sets isConfigOpen to true', () => {
      store.getState().openConfig();

      expect(store.getState().isConfigOpen).toBe(true);
    });

    it('closeConfig sets isConfigOpen to false', () => {
      store.getState().openConfig();
      store.getState().closeConfig();

      expect(store.getState().isConfigOpen).toBe(false);
    });

    it('toggleConfig toggles isConfigOpen from false to true', () => {
      store.getState().toggleConfig();

      expect(store.getState().isConfigOpen).toBe(true);
    });

    it('toggleConfig toggles isConfigOpen from true to false', () => {
      store.getState().openConfig();
      store.getState().toggleConfig();

      expect(store.getState().isConfigOpen).toBe(false);
    });
  });

  describe('Chat Actions', () => {
    it('openChat sets isChatOpen to true', () => {
      store.getState().openChat();

      expect(store.getState().isChatOpen).toBe(true);
    });

    it('closeChat sets isChatOpen to false', () => {
      store.getState().openChat();
      store.getState().closeChat();

      expect(store.getState().isChatOpen).toBe(false);
    });

    it('toggleChat toggles isChatOpen from false to true', () => {
      store.getState().toggleChat();

      expect(store.getState().isChatOpen).toBe(true);
    });

    it('toggleChat toggles isChatOpen from true to false', () => {
      store.getState().openChat();
      store.getState().toggleChat();

      expect(store.getState().isChatOpen).toBe(false);
    });
  });

  describe('Selection Actions', () => {
    it('setSelectedGame updates selectedGameId', () => {
      store.getState().setSelectedGame('game-123');

      expect(store.getState().selectedGameId).toBe('game-123');
    });

    it('setSelectedGame can set to null', () => {
      store.getState().setSelectedGame('game-123');
      store.getState().setSelectedGame(null);

      expect(store.getState().selectedGameId).toBeNull();
    });

    it('setSelectedTypology updates selectedTypologyId', () => {
      store.getState().setSelectedTypology('typology-abc');

      expect(store.getState().selectedTypologyId).toBe('typology-abc');
    });

    it('setSelectedTypology can set to null', () => {
      store.getState().setSelectedTypology('typology-abc');
      store.getState().setSelectedTypology(null);

      expect(store.getState().selectedTypologyId).toBeNull();
    });

    it('setSelectedModel updates selectedModelId', () => {
      store.getState().setSelectedModel('model-xyz');

      expect(store.getState().selectedModelId).toBe('model-xyz');
    });

    it('setSelectedModel can set to null', () => {
      store.getState().setSelectedModel('model-xyz');
      store.getState().setSelectedModel(null);

      expect(store.getState().selectedModelId).toBeNull();
    });

    it('clearSelections resets all selections to null', () => {
      store.getState().setSelectedGame('game-123');
      store.getState().setSelectedTypology('typology-abc');
      store.getState().setSelectedModel('model-xyz');

      store.getState().clearSelections();

      expect(store.getState().selectedGameId).toBeNull();
      expect(store.getState().selectedTypologyId).toBeNull();
      expect(store.getState().selectedModelId).toBeNull();
    });
  });

  describe('Independent State Management', () => {
    it('config and chat states are independent', () => {
      store.getState().openConfig();
      store.getState().openChat();

      expect(store.getState().isConfigOpen).toBe(true);
      expect(store.getState().isChatOpen).toBe(true);

      store.getState().closeConfig();

      expect(store.getState().isConfigOpen).toBe(false);
      expect(store.getState().isChatOpen).toBe(true);
    });

    it('selection states are independent', () => {
      store.getState().setSelectedGame('game-1');
      store.getState().setSelectedTypology('typology-1');

      store.getState().setSelectedGame('game-2');

      expect(store.getState().selectedGameId).toBe('game-2');
      expect(store.getState().selectedTypologyId).toBe('typology-1');
    });

    it('UI state does not affect selection state', () => {
      store.getState().setSelectedGame('game-123');
      store.getState().openConfig();
      store.getState().closeConfig();

      expect(store.getState().selectedGameId).toBe('game-123');
    });
  });
});
