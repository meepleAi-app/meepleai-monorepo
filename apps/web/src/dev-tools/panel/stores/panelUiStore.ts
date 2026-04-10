/**
 * PanelUiState — Dev panel UI store with sessionStorage persistence.
 */

import { createStore } from 'zustand/vanilla';

import type { DevPanelTab } from '@/dev-tools/types';

const VALID_TABS: DevPanelTab[] = ['toggles', 'scenarios', 'auth', 'inspector'];

function safePersist(key: string, value: string): void {
  try {
    sessionStorage.setItem(key, value);
  } catch {
    // QuotaExceededError or private browsing — silently ignore
  }
}

function safeRead(key: string): string | null {
  try {
    return sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

function readInitialState(): {
  isOpen: boolean;
  activeTab: DevPanelTab;
  collapsed: boolean;
  drawerWidth: number;
} {
  const isOpenRaw = safeRead('meepledev-panel-is-open');
  const activeTabRaw = safeRead('meepledev-panel-active-tab');
  const collapsedRaw = safeRead('meepledev-panel-collapsed');
  const drawerWidthRaw = safeRead('meepledev-panel-drawer-width');

  const isOpen = isOpenRaw === 'true';
  const collapsed = collapsedRaw === 'true';
  const drawerWidth = drawerWidthRaw ? parseInt(drawerWidthRaw, 10) || 420 : 420;
  const activeTab: DevPanelTab =
    activeTabRaw && VALID_TABS.includes(activeTabRaw as DevPanelTab)
      ? (activeTabRaw as DevPanelTab)
      : 'toggles';

  return { isOpen, activeTab, collapsed, drawerWidth };
}

export interface PanelUiState {
  open: boolean;
  isOpen: boolean;
  activeTab: DevPanelTab;
  drawerWidth: number;
  collapsed: boolean;
  setOpen: (open: boolean) => void;
  setActiveTab: (tab: DevPanelTab) => void;
  setCollapsed: (collapsed: boolean) => void;
  setDrawerWidth: (width: number) => void;
  toggle: () => void;
}

export function createPanelUiStore() {
  const init = readInitialState();

  return createStore<PanelUiState>((set, get) => ({
    open: init.isOpen,
    isOpen: init.isOpen,
    activeTab: init.activeTab,
    drawerWidth: init.drawerWidth,
    collapsed: init.collapsed,

    setOpen: (open: boolean) => {
      safePersist('meepledev-panel-is-open', String(open));
      set({ open, isOpen: open });
    },

    setActiveTab: (tab: DevPanelTab) => {
      safePersist('meepledev-panel-active-tab', tab);
      set({ activeTab: tab });
    },

    setCollapsed: (collapsed: boolean) => {
      safePersist('meepledev-panel-collapsed', String(collapsed));
      set({ collapsed });
    },

    setDrawerWidth: (width: number) => {
      safePersist('meepledev-panel-drawer-width', String(width));
      set({ drawerWidth: width });
    },

    toggle: () => {
      const next = !get().isOpen;
      safePersist('meepledev-panel-is-open', String(next));
      set({ open: next, isOpen: next });
    },
  }));
}
