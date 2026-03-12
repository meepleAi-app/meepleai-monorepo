import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

export type QuickViewTab = 'rules' | 'faq' | 'ai';
export type QuickViewMode = 'idle' | 'game' | 'session';

interface QuickViewState {
  isOpen: boolean;
  isCollapsed: boolean;
  selectedGameId: string | null;
  sessionId: string | null;
  mode: QuickViewMode;
  activeTab: QuickViewTab;
  openForGame: (gameId: string) => void;
  openForSession: (sessionId: string, gameId: string) => void;
  close: () => void;
  setActiveTab: (tab: QuickViewTab) => void;
  toggleCollapsed: () => void;
}

export const useQuickViewStore = create<QuickViewState>()(
  devtools(
    immer(set => ({
      isOpen: false,
      isCollapsed: false,
      selectedGameId: null,
      sessionId: null,
      mode: 'idle' as QuickViewMode,
      activeTab: 'rules' as QuickViewTab,

      openForGame: (gameId: string) =>
        set(state => {
          state.isOpen = true;
          state.isCollapsed = false;
          state.selectedGameId = gameId;
          state.mode = 'game';
        }),

      openForSession: (sessionId: string, gameId: string) =>
        set(state => {
          state.isOpen = true;
          state.isCollapsed = false;
          state.selectedGameId = gameId;
          state.sessionId = sessionId;
          state.mode = 'session';
        }),

      close: () =>
        set(state => {
          state.isOpen = false;
          state.selectedGameId = null;
          state.sessionId = null;
          state.mode = 'idle';
        }),

      setActiveTab: (tab: QuickViewTab) =>
        set(state => {
          state.activeTab = tab;
        }),

      toggleCollapsed: () =>
        set(state => {
          state.isCollapsed = !state.isCollapsed;
        }),
    })),
    { name: 'quick-view-store' }
  )
);

// Selectors
export const selectIsOpen = (s: QuickViewState) => s.isOpen;
export const selectSelectedGameId = (s: QuickViewState) => s.selectedGameId;
export const selectActiveTab = (s: QuickViewState) => s.activeTab;
export const selectIsCollapsed = (s: QuickViewState) => s.isCollapsed;
export const selectMode = (s: QuickViewState) => s.mode;
export const selectSessionId = (s: QuickViewState) => s.sessionId;
