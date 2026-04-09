/**
 * Mini-nav config store.
 *
 * Holds the current page's mini-nav configuration (breadcrumb, tabs,
 * active tab id, optional primary action) so the global DesktopShell
 * can render it via MiniNavSlot.
 *
 * Pages register their config via the `useMiniNavConfig` hook
 * (Task 3 — `apps/web/src/hooks/useMiniNavConfig.ts`).
 * The config is rendered by `MiniNavSlot` (Task 9 — `DesktopShell/v2/MiniNavSlot.tsx`).
 *
 * The store is framework-agnostic — do NOT add a `'use client'` directive here.
 * Client boundaries live on the consumers (hook + slot).
 */

import { create } from 'zustand';

export interface MiniNavTab {
  id: string;
  label: string;
  href: string;
  count?: number;
}

export interface MiniNavPrimaryAction {
  label: string;
  onClick: () => void;
  icon?: string;
}

export interface MiniNavConfig {
  breadcrumb: string;
  tabs: MiniNavTab[];
  activeTabId: string;
  primaryAction?: MiniNavPrimaryAction;
}

interface MiniNavConfigState {
  config: MiniNavConfig | null;
  setConfig: (config: MiniNavConfig) => void;
  clear: () => void;
}

export const useMiniNavConfigStore = create<MiniNavConfigState>(set => ({
  config: null,
  setConfig: config => set({ config }),
  clear: () => set({ config: null }),
}));
