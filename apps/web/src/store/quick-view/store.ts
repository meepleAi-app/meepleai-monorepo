import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

export type QuickViewTab = 'rules' | 'faq' | 'ai';

interface QuickViewState {
  isOpen: boolean;
  isCollapsed: boolean;
  selectedGameId: string | null;
  activeTab: QuickViewTab;
  openForGame: (gameId: string) => void;
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
      activeTab: 'rules' as QuickViewTab,

      openForGame: (gameId: string) =>
        set(state => {
          state.isOpen = true;
          state.selectedGameId = gameId;
          state.isCollapsed = false;
        }),

      close: () =>
        set(state => {
          state.isOpen = false;
          state.selectedGameId = null;
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
