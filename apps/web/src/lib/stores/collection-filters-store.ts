/**
 * Collection Filters Store (Zustand)
 *
 * Manages filter state for collection dashboard with persistence and optimized updates.
 *
 * Features:
 * - Filter by category, complexity, player count, status
 * - Sort options (recent, alphabetical, rating, duration)
 * - Search query with debouncing
 * - LocalStorage persistence
 * - Reset functionality
 *
 * @see apps/web/src/app/(public)/dashboard/collection-dashboard.tsx
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface CollectionFiltersState {
  // Filters
  categories: string[];
  complexity: number[];
  playerCount: string;
  status: 'all' | 'owned' | 'played' | 'wishlist';
  sortBy: 'recent' | 'alphabetical' | 'rating' | 'duration';
  searchQuery: string;

  // View preferences
  viewMode: 'grid' | 'list';
  sidebarOpen: boolean;

  // Actions
  setCategories: (categories: string[]) => void;
  toggleCategory: (category: string) => void;
  setComplexity: (complexity: number[]) => void;
  toggleComplexity: (level: number) => void;
  setPlayerCount: (count: string) => void;
  setStatus: (status: CollectionFiltersState['status']) => void;
  setSortBy: (sortBy: CollectionFiltersState['sortBy']) => void;
  setSearchQuery: (query: string) => void;
  setViewMode: (mode: 'grid' | 'list') => void;
  toggleSidebar: () => void;
  resetFilters: () => void;
}

const DEFAULT_STATE = {
  categories: [],
  complexity: [],
  playerCount: 'all',
  status: 'all' as const,
  sortBy: 'recent' as const,
  searchQuery: '',
  viewMode: 'grid' as const,
  sidebarOpen: true,
};

export const useCollectionFiltersStore = create<CollectionFiltersState>()(
  persist(
    (set) => ({
      ...DEFAULT_STATE,

      // Filter actions
      setCategories: (categories) => set({ categories }),
      toggleCategory: (category) =>
        set((state) => ({
          categories: state.categories.includes(category)
            ? state.categories.filter((c) => c !== category)
            : [...state.categories, category],
        })),

      setComplexity: (complexity) => set({ complexity }),
      toggleComplexity: (level) =>
        set((state) => ({
          complexity: state.complexity.includes(level)
            ? state.complexity.filter((c) => c !== level)
            : [...state.complexity, level],
        })),

      setPlayerCount: (playerCount) => set({ playerCount }),
      setStatus: (status) => set({ status }),
      setSortBy: (sortBy) => set({ sortBy }),
      setSearchQuery: (searchQuery) => set({ searchQuery }),

      // View actions
      setViewMode: (viewMode) => set({ viewMode }),
      toggleSidebar: () => set((state) => ({ sidebarOpen: !state.sidebarOpen })),

      // Reset
      resetFilters: () => set(DEFAULT_STATE),
    }),
    {
      name: 'collection-filters-storage',
      partialize: (state) => ({
        viewMode: state.viewMode,
        sidebarOpen: state.sidebarOpen,
        sortBy: state.sortBy,
        // Don't persist active filters on page refresh (intentional UX choice)
      }),
    }
  )
);

// Selectors for optimized re-renders
export const selectFilters = (state: CollectionFiltersState) => ({
  categories: state.categories,
  complexity: state.complexity,
  playerCount: state.playerCount,
  status: state.status,
  sortBy: state.sortBy,
  searchQuery: state.searchQuery,
});

export const selectViewPreferences = (state: CollectionFiltersState) => ({
  viewMode: state.viewMode,
  sidebarOpen: state.sidebarOpen,
});

export const selectHasActiveFilters = (state: CollectionFiltersState) =>
  state.categories.length > 0 ||
  state.complexity.length > 0 ||
  state.playerCount !== 'all' ||
  state.status !== 'all' ||
  state.searchQuery.length > 0;
