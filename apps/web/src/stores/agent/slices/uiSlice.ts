/**
 * UI Slice - Agent UI State Management
 * Issue #3238 (FRONT-002)
 * Issue #3376 - Added strategy and tier selection
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

  // Strategy & Tier Selection (Issue #3376)
  selectedStrategyId: string | null;
  selectedTierId: string | null;

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

  // Strategy & Tier Actions (Issue #3376)
  setSelectedStrategy: (strategyId: string | null) => void;
  setSelectedTier: (tierId: string | null) => void;
}

export const createUISlice: StateCreator<AgentStore, [], [], UISlice> = set => ({
  // Initial state
  isConfigOpen: false,
  isChatOpen: false,
  selectedGameId: null,
  selectedTypologyId: null,
  selectedModelId: null,
  selectedStrategyId: 'BALANCED', // Default strategy
  selectedTierId: 'free', // Default tier

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
    set({
      selectedGameId: null,
      selectedTypologyId: null,
      selectedModelId: null,
      selectedStrategyId: 'BALANCED',
      selectedTierId: 'free',
    }),

  // Strategy & Tier actions (Issue #3376)
  setSelectedStrategy: strategyId => set({ selectedStrategyId: strategyId }),
  setSelectedTier: tierId => set({ selectedTierId: tierId }),
});
