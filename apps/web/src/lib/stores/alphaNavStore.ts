/**
 * Alpha Navigation Store
 *
 * Lightweight Zustand store for alpha layout tab navigation,
 * detail modal state, and section title management.
 * Ephemeral state — no persist middleware needed.
 */

import { create } from 'zustand';

// ============================================================================
// Types
// ============================================================================

export type AlphaTab = 'home' | 'library' | 'play' | 'chat';

interface AlphaNavState {
  activeTab: AlphaTab;
  setActiveTab: (tab: AlphaTab) => void;

  // Detail modal
  detailEntityId: string | null;
  detailEntityType: string | null;
  openDetail: (entityId: string, entityType: string) => void;
  closeDetail: () => void;

  // Section title (shown in TopNav)
  sectionTitle: string;
  setSectionTitle: (title: string) => void;
}

// ============================================================================
// Store
// ============================================================================

export const useAlphaNavStore = create<AlphaNavState>(set => ({
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
