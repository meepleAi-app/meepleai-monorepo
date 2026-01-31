/**
 * UI Slice - Agent UI State Management
 * Issue #3238 (FRONT-002)
 *
 * Manages ephemeral UI state (not persisted)
 */

import type { StateCreator } from 'zustand';
import type { AgentStore } from '../types/store.types';

export interface UISlice {
  // UI State
  isConfigOpen: boolean;
  isChatOpen: boolean;

  // Actions
  openConfig: () => void;
  closeConfig: () => void;
  toggleConfig: () => void;
  openChat: () => void;
  closeChat: () => void;
  toggleChat: () => void;
}

export const createUISlice: StateCreator<AgentStore, [], [], UISlice> = set => ({
  // Initial state
  isConfigOpen: false,
  isChatOpen: false,

  // Config actions
  openConfig: () => set({ isConfigOpen: true }),
  closeConfig: () => set({ isConfigOpen: false }),
  toggleConfig: () => set(state => ({ isConfigOpen: !state.isConfigOpen })),

  // Chat actions
  openChat: () => set({ isChatOpen: true }),
  closeChat: () => set({ isChatOpen: false }),
  toggleChat: () => set(state => ({ isChatOpen: !state.isChatOpen })),
});
