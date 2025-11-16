/**
 * Session Slice (Issue #1083)
 *
 * Manages user session state:
 * - Selected game and agent
 * - Sidebar UI state
 *
 * This slice is independent and has no dependencies on other slices.
 */

import { StateCreator } from 'zustand';
import { ChatStore, SessionSlice } from '../types';

export const createSessionSlice: StateCreator<
  ChatStore,
  [['zustand/immer', never], ['zustand/subscribeWithSelector', never]],
  [],
  SessionSlice
> = (set) => ({
  // ============================================================================
  // State
  // ============================================================================
  selectedGameId: null,
  selectedAgentId: null,
  sidebarCollapsed: false,

  // ============================================================================
  // Actions
  // ============================================================================
  selectGame: (gameId) =>
    set((state) => {
      const previousGameId = state.selectedGameId;
      state.selectedGameId = gameId;
      // Reset agent when game changes
      if (gameId !== previousGameId) {
        state.selectedAgentId = null;
      }
    }),

  selectAgent: (agentId) =>
    set((state) => {
      state.selectedAgentId = agentId;
    }),

  toggleSidebar: () =>
    set((state) => {
      state.sidebarCollapsed = !state.sidebarCollapsed;
    }),

  setSidebarCollapsed: (collapsed) =>
    set((state) => {
      state.sidebarCollapsed = collapsed;
    }),
});