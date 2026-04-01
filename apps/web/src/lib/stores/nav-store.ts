/**
 * Navigation Store
 *
 * Lightweight Zustand store for layout tab navigation,
 * detail modal state, and section title management.
 * Ephemeral state — no persist middleware needed.
 */

import { create } from 'zustand';

export type NavTab = 'home' | 'library' | 'play' | 'chat';

interface NavState {
  activeTab: NavTab;
  setActiveTab: (tab: NavTab) => void;

  detailEntityId: string | null;
  detailEntityType: string | null;
  openDetail: (entityId: string, entityType: string) => void;
  closeDetail: () => void;

  sectionTitle: string;
  setSectionTitle: (title: string) => void;
}

export const useNavStore = create<NavState>(set => ({
  activeTab: 'home',
  setActiveTab: tab => set({ activeTab: tab }),

  detailEntityId: null,
  detailEntityType: null,
  openDetail: (entityId, entityType) =>
    set({ detailEntityId: entityId, detailEntityType: entityType }),
  closeDetail: () => set({ detailEntityId: null, detailEntityType: null }),

  sectionTitle: 'Home',
  setSectionTitle: title => set({ sectionTitle: title }),
}));
