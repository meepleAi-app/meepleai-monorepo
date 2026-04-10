/**
 * PanelUiState — Dev panel UI store (drawer open/close, active tab, width).
 */
import { createStore } from 'zustand/vanilla';

export interface PanelUiState {
  isOpen: boolean;
  activeTab: string;
  drawerWidth: number;
  open: boolean;
  setOpen: (open: boolean) => void;
  toggle: () => void;
  setActiveTab: (tab: string) => void;
}

export function createPanelUiStore() {
  return createStore<PanelUiState>(set => ({
    isOpen: false,
    activeTab: 'toggles',
    drawerWidth: 360,
    get open() {
      return this.isOpen;
    },
    setOpen: (open: boolean) => set({ isOpen: open, open }),
    toggle: () => set(s => ({ isOpen: !s.isOpen, open: !s.isOpen })),
    setActiveTab: (tab: string) => set({ activeTab: tab }),
  }));
}
