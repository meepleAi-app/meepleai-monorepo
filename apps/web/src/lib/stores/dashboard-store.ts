/**
 * Gaming Hub Dashboard Store - Issue #4583
 * Epic #4575: Gaming Hub Dashboard - Phase 2
 *
 * Zustand store for dashboard state management
 */

import { create } from 'zustand';

import {
  dashboardClient,
  type UserStatsDto,
  type SessionSummaryDto,
  type UserGameDto,
  type GetUserGamesParams,
} from '@/lib/api/dashboard-client';

// ============================================================================
// Types
// ============================================================================

interface DashboardFilters {
  search: string;
  category: string;
  sort: 'alphabetical' | 'playCount';
  page: number;
  pageSize: number;
}

interface DashboardState {
  // Data
  stats: UserStatsDto | null;
  recentSessions: SessionSummaryDto[];
  games: UserGameDto[];
  totalGamesCount: number;
  filters: DashboardFilters;

  // Loading states
  isLoadingStats: boolean;
  isLoadingSessions: boolean;
  isLoadingGames: boolean;

  // Errors
  statsError: string | null;
  sessionsError: string | null;
  gamesError: string | null;

  // Actions
  fetchStats: () => Promise<void>;
  fetchRecentSessions: (limit?: number) => Promise<void>;
  fetchGames: () => Promise<void>;
  updateFilters: (filters: Partial<DashboardFilters>) => void;
  loadMore: () => Promise<void>;
  reset: () => void;
}

// ============================================================================
// Store Implementation
// ============================================================================

export const useDashboardStore = create<DashboardState>((set, get) => ({
  // Initial state
  stats: null,
  recentSessions: [],
  games: [],
  totalGamesCount: 0,
  filters: {
    search: '',
    category: 'all',
    sort: 'alphabetical',
    page: 1,
    pageSize: 20,
  },
  isLoadingStats: false,
  isLoadingSessions: false,
  isLoadingGames: false,
  statsError: null,
  sessionsError: null,
  gamesError: null,

  // Actions
  fetchStats: async () => {
    set({ isLoadingStats: true, statsError: null });
    try {
      const stats = await dashboardClient.getUserStats();
      set({ stats, isLoadingStats: false });
    } catch (error) {
      set({
        statsError: error instanceof Error ? error.message : 'Failed to load stats',
        isLoadingStats: false,
      });
    }
  },

  fetchRecentSessions: async (limit = 3) => {
    set({ isLoadingSessions: true, sessionsError: null });
    try {
      const sessions = await dashboardClient.getRecentSessions(limit);
      set({ recentSessions: sessions, isLoadingSessions: false });
    } catch (error) {
      set({
        sessionsError:
          error instanceof Error ? error.message : 'Failed to load sessions',
        isLoadingSessions: false,
      });
    }
  },

  fetchGames: async () => {
    const { filters } = get();
    set({ isLoadingGames: true, gamesError: null });

    try {
      const params: GetUserGamesParams = {
        category: filters.category === 'all' ? undefined : filters.category,
        sort: filters.sort,
        page: filters.page,
        pageSize: filters.pageSize,
      };

      const result = await dashboardClient.getUserGames(params);
      set({
        games: result.items,
        totalGamesCount: result.totalCount,
        isLoadingGames: false,
      });
    } catch (error) {
      set({
        gamesError: error instanceof Error ? error.message : 'Failed to load games',
        isLoadingGames: false,
      });
    }
  },

  updateFilters: (newFilters) => {
    set((state) => ({
      filters: { ...state.filters, ...newFilters, page: 1 }, // Reset page on filter change
    }));
    // Re-fetch with new filters
    get().fetchGames();
  },

  loadMore: async () => {
    const { filters: _filters } = get();
    set((state) => ({
      filters: { ...state.filters, page: state.filters.page + 1 },
    }));
    await get().fetchGames();
  },

  reset: () => {
    set({
      stats: null,
      recentSessions: [],
      games: [],
      totalGamesCount: 0,
      filters: {
        search: '',
        category: 'all',
        sort: 'alphabetical',
        page: 1,
        pageSize: 20,
      },
      isLoadingStats: false,
      isLoadingSessions: false,
      isLoadingGames: false,
      statsError: null,
      sessionsError: null,
      gamesError: null,
    });
  },
}));
