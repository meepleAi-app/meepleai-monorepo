/**
 * PanelUiState — Dev panel UI store.
 *
 * Phase 2 M0 stub expanded with properties consumed by
 * DevPanel, DevPanelMount, and installPanel.
 */

import { createStore } from 'zustand/vanilla';

import type { DevPanelTab } from '@/dev-tools/types';

export interface PanelUiState {
  open: boolean;
  isOpen: boolean;
  activeTab: DevPanelTab;
  drawerWidth: number;
  setOpen: (open: boolean) => void;
  setActiveTab: (tab: DevPanelTab) => void;
  toggle: () => void;
}

export function createPanelUiStore() {
  return createStore<PanelUiState>((set, get) => ({
    open: false,
    isOpen: false,
    activeTab: 'toggles' as DevPanelTab,
    drawerWidth: 420,
    setOpen: (open: boolean) => set({ open, isOpen: open }),
    setActiveTab: (tab: DevPanelTab) => set({ activeTab: tab }),
    toggle: () => {
      const next = !get().isOpen;
      set({ open: next, isOpen: next });
    },
  }));
}
