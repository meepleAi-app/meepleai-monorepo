/**
 * Admin Games Store (Issue #2372)
 *
 * Manages admin shared games state:
 * - Games list with filters and pagination
 * - Categories and mechanics for filters
 * - Pending delete requests count
 * - Selected game for editing
 *
 * Middleware Stack:
 * - devtools: Browser DevTools integration
 * - immer: Mutable state updates
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import type {
  SharedGame,
  SharedGameDetail,
  GameCategory,
  GameMechanic,
  DeleteRequest,
  PagedSharedGames,
  PagedDeleteRequests,
} from '@/lib/api/schemas/shared-games.schemas';

// ============================================================================
// Store State Interface
// ============================================================================

export interface AdminGamesFilters {
  searchTerm: string;
  status: number | null;
  categoryIds: string | null;
  mechanicIds: string | null;
  sortBy: string;
  sortDescending: boolean;
}

export interface AdminGamesState {
  // Games list
  games: SharedGame[];
  total: number;
  page: number;
  pageSize: number;
  filters: AdminGamesFilters;

  // Reference data
  categories: GameCategory[];
  mechanics: GameMechanic[];

  // Pending deletes
  pendingDeleteRequests: DeleteRequest[];
  pendingDeletesCount: number;

  // Selected game for editing
  selectedGame: SharedGameDetail | null;

  // Loading states
  isLoadingGames: boolean;
  isLoadingCategories: boolean;
  isLoadingMechanics: boolean;
  isLoadingPendingDeletes: boolean;
  isLoadingSelectedGame: boolean;

  // Error state
  error: string | null;

  // Actions - Games
  setGames: (data: PagedSharedGames) => void;
  setPage: (page: number) => void;
  setPageSize: (pageSize: number) => void;
  setFilters: (filters: Partial<AdminGamesFilters>) => void;
  resetFilters: () => void;

  // Actions - Reference Data
  setCategories: (categories: GameCategory[]) => void;
  setMechanics: (mechanics: GameMechanic[]) => void;

  // Actions - Pending Deletes
  setPendingDeletes: (data: PagedDeleteRequests) => void;
  decrementPendingDeletesCount: () => void;

  // Actions - Selected Game
  setSelectedGame: (game: SharedGameDetail | null) => void;

  // Actions - Loading
  setLoadingGames: (loading: boolean) => void;
  setLoadingCategories: (loading: boolean) => void;
  setLoadingMechanics: (loading: boolean) => void;
  setLoadingPendingDeletes: (loading: boolean) => void;
  setLoadingSelectedGame: (loading: boolean) => void;

  // Actions - Error
  setError: (error: string | null) => void;
  clearError: () => void;

  // Actions - Utility
  reset: () => void;
}

// ============================================================================
// Default Filters
// ============================================================================

const defaultFilters: AdminGamesFilters = {
  searchTerm: '',
  status: null,
  categoryIds: null,
  mechanicIds: null,
  sortBy: 'title',
  sortDescending: false,
};

// ============================================================================
// Store Creation
// ============================================================================

export const useAdminGamesStore = create<AdminGamesState>()(
  devtools(
    immer(set => ({
      // Initial state
      games: [],
      total: 0,
      page: 1,
      pageSize: 20,
      filters: { ...defaultFilters },

      categories: [],
      mechanics: [],

      pendingDeleteRequests: [],
      pendingDeletesCount: 0,

      selectedGame: null,

      isLoadingGames: false,
      isLoadingCategories: false,
      isLoadingMechanics: false,
      isLoadingPendingDeletes: false,
      isLoadingSelectedGame: false,

      error: null,

      // Games actions
      setGames: (data: PagedSharedGames) => {
        set(state => {
          state.games = data.items;
          state.total = data.total;
          state.page = data.page;
          state.pageSize = data.pageSize;
          state.isLoadingGames = false;
        });
      },

      setPage: (page: number) => {
        set(state => {
          state.page = page;
        });
      },

      setPageSize: (pageSize: number) => {
        set(state => {
          state.pageSize = pageSize;
          state.page = 1; // Reset to first page when changing page size
        });
      },

      setFilters: (filters: Partial<AdminGamesFilters>) => {
        set(state => {
          state.filters = { ...state.filters, ...filters };
          state.page = 1; // Reset to first page when filters change
        });
      },

      resetFilters: () => {
        set(state => {
          state.filters = { ...defaultFilters };
          state.page = 1;
        });
      },

      // Reference data actions
      setCategories: (categories: GameCategory[]) => {
        set(state => {
          state.categories = categories;
          state.isLoadingCategories = false;
        });
      },

      setMechanics: (mechanics: GameMechanic[]) => {
        set(state => {
          state.mechanics = mechanics;
          state.isLoadingMechanics = false;
        });
      },

      // Pending deletes actions
      setPendingDeletes: (data: PagedDeleteRequests) => {
        set(state => {
          state.pendingDeleteRequests = data.items;
          state.pendingDeletesCount = data.total;
          state.isLoadingPendingDeletes = false;
        });
      },

      decrementPendingDeletesCount: () => {
        set(state => {
          state.pendingDeletesCount = Math.max(0, state.pendingDeletesCount - 1);
        });
      },

      // Selected game actions
      setSelectedGame: (game: SharedGameDetail | null) => {
        set(state => {
          state.selectedGame = game;
          state.isLoadingSelectedGame = false;
        });
      },

      // Loading actions
      setLoadingGames: (loading: boolean) => {
        set(state => {
          state.isLoadingGames = loading;
          if (loading) state.error = null;
        });
      },

      setLoadingCategories: (loading: boolean) => {
        set(state => {
          state.isLoadingCategories = loading;
        });
      },

      setLoadingMechanics: (loading: boolean) => {
        set(state => {
          state.isLoadingMechanics = loading;
        });
      },

      setLoadingPendingDeletes: (loading: boolean) => {
        set(state => {
          state.isLoadingPendingDeletes = loading;
        });
      },

      setLoadingSelectedGame: (loading: boolean) => {
        set(state => {
          state.isLoadingSelectedGame = loading;
          if (loading) state.selectedGame = null;
        });
      },

      // Error actions
      setError: (error: string | null) => {
        set(state => {
          state.error = error;
          state.isLoadingGames = false;
          state.isLoadingSelectedGame = false;
        });
      },

      clearError: () => {
        set(state => {
          state.error = null;
        });
      },

      // Reset store
      reset: () => {
        set(state => {
          state.games = [];
          state.total = 0;
          state.page = 1;
          state.pageSize = 20;
          state.filters = { ...defaultFilters };
          state.categories = [];
          state.mechanics = [];
          state.pendingDeleteRequests = [];
          state.pendingDeletesCount = 0;
          state.selectedGame = null;
          state.isLoadingGames = false;
          state.isLoadingCategories = false;
          state.isLoadingMechanics = false;
          state.isLoadingPendingDeletes = false;
          state.isLoadingSelectedGame = false;
          state.error = null;
        });
      },
    })),
    {
      name: 'AdminGamesStore', // DevTools name
    }
  )
);

// ============================================================================
// Selectors for Optimized Subscriptions
// ============================================================================

export const selectGames = (state: AdminGamesState) => state.games;
export const selectTotal = (state: AdminGamesState) => state.total;
export const selectPage = (state: AdminGamesState) => state.page;
export const selectPageSize = (state: AdminGamesState) => state.pageSize;
export const selectFilters = (state: AdminGamesState) => state.filters;

export const selectCategories = (state: AdminGamesState) => state.categories;
export const selectMechanics = (state: AdminGamesState) => state.mechanics;

export const selectPendingDeletesCount = (state: AdminGamesState) => state.pendingDeletesCount;
export const selectPendingDeleteRequests = (state: AdminGamesState) => state.pendingDeleteRequests;

export const selectSelectedGame = (state: AdminGamesState) => state.selectedGame;

export const selectIsLoadingGames = (state: AdminGamesState) => state.isLoadingGames;
export const selectIsLoadingAnyReferenceData = (state: AdminGamesState) =>
  state.isLoadingCategories || state.isLoadingMechanics;

export const selectError = (state: AdminGamesState) => state.error;

// Derived selectors
export const selectTotalPages = (state: AdminGamesState) => Math.ceil(state.total / state.pageSize);

export const selectHasActiveFilters = (state: AdminGamesState) =>
  state.filters.searchTerm !== '' ||
  state.filters.status !== null ||
  state.filters.categoryIds !== null ||
  state.filters.mechanicIds !== null;
