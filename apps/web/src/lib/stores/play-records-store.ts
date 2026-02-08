/**
 * Play Records Store (Zustand)
 *
 * Manages state for play records feature:
 * - Session creation wizard state
 * - Play history filters
 * - View preferences (grid/list, sort order)
 *
 * Issue #3892: Play Records Frontend UI
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { PlayRecordStatus, PlayRecordVisibility } from '@/lib/api/schemas/play-records.schemas';

// ========== State Interfaces ==========

export interface SessionCreationState {
  currentStep: number;
  gameType: 'catalog' | 'freeform';
  gameId?: string;
  gameName: string;
  sessionDate: Date;
  visibility: PlayRecordVisibility;
  groupId?: string;
  enableScoring: boolean;
  scoringDimensions: string[];
  dimensionUnits: Record<string, string>;
  notes?: string;
  location?: string;
}

export interface PlayHistoryFilters {
  gameId?: string;
  status: PlayRecordStatus | 'all';
  dateFrom?: string;
  dateTo?: string;
  searchQuery: string;
}

export interface PlayRecordsStoreState {
  // Session Creation
  sessionCreation: SessionCreationState;
  setSessionField: <K extends keyof SessionCreationState>(
    field: K,
    value: SessionCreationState[K]
  ) => void;
  nextStep: () => void;
  prevStep: () => void;
  resetSessionCreation: () => void;

  // Play History Filters
  filters: PlayHistoryFilters;
  setFilter: <K extends keyof PlayHistoryFilters>(
    field: K,
    value: PlayHistoryFilters[K]
  ) => void;
  resetFilters: () => void;

  // View Preferences
  viewMode: 'grid' | 'list';
  sortBy: 'recent' | 'oldest' | 'game' | 'duration';
  sidebarOpen: boolean;
  setViewMode: (mode: 'grid' | 'list') => void;
  setSortBy: (sort: PlayRecordsStoreState['sortBy']) => void;
  toggleSidebar: () => void;
}

// ========== Default State ==========

const DEFAULT_SESSION_CREATION: SessionCreationState = {
  currentStep: 0,
  gameType: 'catalog',
  gameName: '',
  sessionDate: new Date(),
  visibility: 'Private',
  enableScoring: false,
  scoringDimensions: [],
  dimensionUnits: {},
};

const DEFAULT_FILTERS: PlayHistoryFilters = {
  status: 'all',
  searchQuery: '',
};

// ========== Store Implementation ==========

export const usePlayRecordsStore = create<PlayRecordsStoreState>()(
  persist(
    (set) => ({
      // Session Creation State
      sessionCreation: DEFAULT_SESSION_CREATION,

      setSessionField: (field, value) =>
        set((state) => ({
          sessionCreation: {
            ...state.sessionCreation,
            [field]: value,
          },
        })),

      nextStep: () =>
        set((state) => ({
          sessionCreation: {
            ...state.sessionCreation,
            currentStep: Math.min(state.sessionCreation.currentStep + 1, 3),
          },
        })),

      prevStep: () =>
        set((state) => ({
          sessionCreation: {
            ...state.sessionCreation,
            currentStep: Math.max(state.sessionCreation.currentStep - 1, 0),
          },
        })),

      resetSessionCreation: () =>
        set(() => ({
          sessionCreation: DEFAULT_SESSION_CREATION,
        })),

      // Play History Filters
      filters: DEFAULT_FILTERS,

      setFilter: (field, value) =>
        set((state) => ({
          filters: {
            ...state.filters,
            [field]: value,
          },
        })),

      resetFilters: () =>
        set(() => ({
          filters: DEFAULT_FILTERS,
        })),

      // View Preferences
      viewMode: 'grid',
      sortBy: 'recent',
      sidebarOpen: true,

      setViewMode: (viewMode) => set({ viewMode }),
      setSortBy: (sortBy) => set({ sortBy }),
      toggleSidebar: () => set((state) => ({ sidebarOpen: !state.sidebarOpen })),
    }),
    {
      name: 'play-records-storage',
      // Only persist view preferences, not active filters or creation state
      partialize: (state) => ({
        viewMode: state.viewMode,
        sortBy: state.sortBy,
        sidebarOpen: state.sidebarOpen,
      }),
    }
  )
);

// ========== Selectors ==========

export const selectSessionCreation = (state: PlayRecordsStoreState) => state.sessionCreation;

export const selectFilters = (state: PlayRecordsStoreState) => state.filters;

export const selectViewPreferences = (state: PlayRecordsStoreState) => ({
  viewMode: state.viewMode,
  sortBy: state.sortBy,
  sidebarOpen: state.sidebarOpen,
});

export const selectHasActiveFilters = (state: PlayRecordsStoreState) =>
  state.filters.gameId !== undefined ||
  state.filters.status !== 'all' ||
  state.filters.dateFrom !== undefined ||
  state.filters.dateTo !== undefined ||
  state.filters.searchQuery.length > 0;

export const selectCanProceedToNextStep = (state: PlayRecordsStoreState) => {
  const { currentStep, gameType, gameId, gameName } = state.sessionCreation;

  switch (currentStep) {
    case 0: // Game selection
      return gameType === 'catalog' ? !!gameId : gameName.length > 0;
    case 1: // Date and details
      return true; // Date has default, notes/location optional
    case 2: // Players (optional step)
      return true;
    default:
      return false;
  }
};
