/**
 * UI Slice - Agent UI State Management
 * Issue #3238 (FRONT-002)
 *
 * Manages ephemeral UI state (not persisted)
 */

import type { AgentStore } from '../types/store.types';
import type { StateCreator } from 'zustand';

export interface UISlice {
  // UI State
  isConfigOpen: boolean;
  isChatOpen: boolean;

  // Selection State (Issue #3239)
  selectedGameId: string | null;
  selectedTypologyId: string | null;
  selectedModelId: string | null;

  // Actions
  openConfig: () => void;
  closeConfig: () => void;
  toggleConfig: () => void;
  openChat: () => void;
  closeChat: () => void;
  toggleChat: () => void;

  // Selection Actions (Issue #3239)
  setSelectedGame: (gameId: string | null) => void;
  setSelectedTypology: (typologyId: string | null) => void;
  setSelectedModel: (modelId: string | null) => void;
  clearSelections: () => void;
}

export const createUISlice: StateCreator<AgentStore, [], [], UISlice> = set => ({
  // Initial state
  isConfigOpen: false,
  isChatOpen: false,
  selectedGameId: null,
  selectedTypologyId: null,
  selectedModelId: null,

  // Config actions
  openConfig: () => set({ isConfigOpen: true }),
  closeConfig: () => set({ isConfigOpen: false }),
  toggleConfig: () => set(state => ({ isConfigOpen: !state.isConfigOpen })),

  // Chat actions
  openChat: () => set({ isChatOpen: true }),
  closeChat: () => set({ isChatOpen: false }),
  toggleChat: () => set(state => ({ isChatOpen: !state.isChatOpen })),

  // Selection actions (Issue #3239)
  setSelectedGame: gameId => set({ selectedGameId: gameId }),
  setSelectedTypology: typologyId => set({ selectedTypologyId: typologyId }),
  setSelectedModel: modelId => set({ selectedModelId: modelId }),
  clearSelections: () =>
    set({ selectedGameId: null, selectedTypologyId: null, selectedModelId: null }),
});
