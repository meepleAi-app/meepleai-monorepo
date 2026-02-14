/**
 * Minimal Agent Config Store (Issue #4368)
 *
 * Lightweight replacement for the deleted stores/agent/ directory.
 * Provides only the config state needed by agent config components
 * (GameSelector, ModelTierSelector, CostPreview, etc.)
 */

import { create } from 'zustand';

interface AgentConfigState {
  isConfigOpen: boolean;
  openConfig: () => void;
  closeConfig: () => void;
  selectedGameId: string | null;
  selectedTypologyId: string | null;
  selectedModelId: string | null;
  selectedTierId: string | null;
  setSelectedGame: (gameId: string | null) => void;
  setSelectedTypology: (typologyId: string | null) => void;
  setSelectedModel: (modelId: string | null) => void;
  setSelectedTier: (tierId: string | null) => void;
  reset: () => void;
}

export const useAgentStore = create<AgentConfigState>()((set) => ({
  isConfigOpen: false,
  openConfig: () => set({ isConfigOpen: true }),
  closeConfig: () => set({ isConfigOpen: false }),
  selectedGameId: null,
  selectedTypologyId: null,
  selectedModelId: null,
  selectedTierId: null,
  setSelectedGame: (gameId) => set({ selectedGameId: gameId }),
  setSelectedTypology: (typologyId) => set({ selectedTypologyId: typologyId }),
  setSelectedModel: (modelId) => set({ selectedModelId: modelId }),
  setSelectedTier: (tierId) => set({ selectedTierId: tierId }),
  reset: () =>
    set({
      isConfigOpen: false,
      selectedGameId: null,
      selectedTypologyId: null,
      selectedModelId: null,
      selectedTierId: null,
    }),
}));

// Re-export type for backwards compatibility
export type AgentStore = AgentConfigState;
