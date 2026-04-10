import { createStore, type StoreApi } from 'zustand/vanilla';

import type { DevPanelTab } from '@/dev-tools/types';

export interface PanelUiState {
  isOpen: boolean;
  collapsed: boolean;
  activeTab: DevPanelTab;
  drawerWidth: number;

  setOpen: (open: boolean) => void;
  toggle: () => void;
  setCollapsed: (c: boolean) => void;
  setActiveTab: (tab: DevPanelTab) => void;
  setDrawerWidth: (w: number) => void;
}

const KEYS = {
  isOpen: 'meepledev-panel-is-open',
  collapsed: 'meepledev-panel-collapsed',
  activeTab: 'meepledev-panel-active-tab',
  drawerWidth: 'meepledev-panel-drawer-width',
} as const;

const VALID_TABS: DevPanelTab[] = ['toggles', 'scenarios', 'auth', 'inspector'];

function safeRead(key: string): string | null {
  try {
    return typeof sessionStorage !== 'undefined' ? sessionStorage.getItem(key) : null;
  } catch {
    return null;
  }
}

function safeWrite(key: string, value: string): void {
  try {
    if (typeof sessionStorage !== 'undefined') {
      sessionStorage.setItem(key, value);
    }
  } catch {
    /* quota exceeded — in-memory fallback */
  }
}

function readBool(key: string, defaultValue: boolean): boolean {
  const raw = safeRead(key);
  if (raw === 'true') return true;
  if (raw === 'false') return false;
  return defaultValue;
}

function readTab(key: string, defaultValue: DevPanelTab): DevPanelTab {
  const raw = safeRead(key);
  return VALID_TABS.includes(raw as DevPanelTab) ? (raw as DevPanelTab) : defaultValue;
}

function readInt(key: string, defaultValue: number): number {
  const raw = safeRead(key);
  const parsed = raw ? parseInt(raw, 10) : NaN;
  return Number.isFinite(parsed) ? parsed : defaultValue;
}

export function createPanelUiStore(): StoreApi<PanelUiState> {
  return createStore<PanelUiState>((set, get) => ({
    isOpen: readBool(KEYS.isOpen, false),
    collapsed: readBool(KEYS.collapsed, false),
    activeTab: readTab(KEYS.activeTab, 'toggles'),
    drawerWidth: readInt(KEYS.drawerWidth, 420),

    setOpen: (open: boolean) => {
      safeWrite(KEYS.isOpen, String(open));
      set({ isOpen: open });
    },
    toggle: () => {
      const next = !get().isOpen;
      safeWrite(KEYS.isOpen, String(next));
      set({ isOpen: next });
    },
    setCollapsed: (c: boolean) => {
      safeWrite(KEYS.collapsed, String(c));
      set({ collapsed: c });
    },
    setActiveTab: (tab: DevPanelTab) => {
      safeWrite(KEYS.activeTab, tab);
      set({ activeTab: tab });
    },
    setDrawerWidth: (w: number) => {
      safeWrite(KEYS.drawerWidth, String(w));
      set({ drawerWidth: w });
    },
  }));
}
