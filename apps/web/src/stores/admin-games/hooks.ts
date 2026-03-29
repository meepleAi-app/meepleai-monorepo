/**
 * Admin Games Store Hooks (Issue #2372)
 *
 * Custom hooks for admin games state management with
 * API integration and optimized selectors.
 */

import { useCallback, useEffect } from 'react';

import { useApiClient } from '@/lib/api/context';
import { logger } from '@/lib/logger';

import {
  useAdminGamesStore,
  selectGames,
  selectTotal,
  selectPage,
  selectPageSize,
  selectFilters,
  selectCategories,
  selectMechanics,
  selectPendingDeletesCount,
  selectSelectedGame,
  selectIsLoadingGames,
  selectIsLoadingAnyReferenceData,
  selectTotalPages,
  selectHasActiveFilters,
  selectError,
} from './store';

// ============================================================================
// Optimized Selector Hooks
// ============================================================================

export const useAdminGames = () => useAdminGamesStore(selectGames);
export const useAdminGamesTotal = () => useAdminGamesStore(selectTotal);
export const useAdminGamesPage = () => useAdminGamesStore(selectPage);
export const useAdminGamesPageSize = () => useAdminGamesStore(selectPageSize);
export const useAdminGamesFilters = () => useAdminGamesStore(selectFilters);

export const useGameCategories = () => useAdminGamesStore(selectCategories);
export const useGameMechanics = () => useAdminGamesStore(selectMechanics);

export const usePendingDeletesCount = () => useAdminGamesStore(selectPendingDeletesCount);

export const useSelectedGame = () => useAdminGamesStore(selectSelectedGame);

export const useIsLoadingGames = () => useAdminGamesStore(selectIsLoadingGames);
export const useIsLoadingReferenceData = () => useAdminGamesStore(selectIsLoadingAnyReferenceData);

export const useTotalPages = () => useAdminGamesStore(selectTotalPages);
export const useHasActiveFilters = () => useAdminGamesStore(selectHasActiveFilters);
export const useAdminGamesError = () => useAdminGamesStore(selectError);

// ============================================================================
// Data Fetching Hooks
// ============================================================================

/**
 * Hook for fetching admin games list with current filters
 */
export function useAdminGamesData() {
  const { sharedGames } = useApiClient();
  const { setGames, setLoadingGames, setError, page, pageSize, filters } = useAdminGamesStore();

  const fetchGames = useCallback(async () => {
    setLoadingGames(true);
    try {
      const params: Parameters<typeof sharedGames.search>[0] = {
        page,
        pageSize,
        sortBy: filters.sortBy,
        sortDescending: filters.sortDescending,
      };

      if (filters.searchTerm) params.searchTerm = filters.searchTerm;
      if (filters.status !== null) params.status = filters.status;
      if (filters.categoryIds) params.categoryIds = filters.categoryIds;
      if (filters.mechanicIds) params.mechanicIds = filters.mechanicIds;

      // Use admin endpoint to get all statuses
      const result = await sharedGames.getAll({
        page,
        pageSize,
        status: filters.status ?? undefined,
      });

      setGames(result);
    } catch (error) {
      logger.error('Failed to fetch admin games:', error);
      setError(error instanceof Error ? error.message : 'Failed to fetch games');
    }
  }, [sharedGames, page, pageSize, filters, setGames, setLoadingGames, setError]);

  return { fetchGames };
}

/**
 * Hook for fetching reference data (categories and mechanics)
 */
export function useReferenceData() {
  const { sharedGames } = useApiClient();
  const {
    setCategories,
    setMechanics,
    setLoadingCategories,
    setLoadingMechanics,
    categories,
    mechanics,
  } = useAdminGamesStore();

  const fetchCategories = useCallback(async () => {
    if (categories.length > 0) return; // Already loaded

    setLoadingCategories(true);
    try {
      const result = await sharedGames.getCategories();
      setCategories(result);
    } catch (error) {
      logger.error('Failed to fetch categories:', error);
      // Non-critical, don't set error state
    }
  }, [sharedGames, categories.length, setCategories, setLoadingCategories]);

  const fetchMechanics = useCallback(async () => {
    if (mechanics.length > 0) return; // Already loaded

    setLoadingMechanics(true);
    try {
      const result = await sharedGames.getMechanics();
      setMechanics(result);
    } catch (error) {
      logger.error('Failed to fetch mechanics:', error);
      // Non-critical, don't set error state
    }
  }, [sharedGames, mechanics.length, setMechanics, setLoadingMechanics]);

  const fetchAll = useCallback(async () => {
    await Promise.all([fetchCategories(), fetchMechanics()]);
  }, [fetchCategories, fetchMechanics]);

  return { fetchCategories, fetchMechanics, fetchAll };
}

/**
 * Hook for fetching pending deletes count
 */
export function usePendingDeletesData() {
  const { sharedGames } = useApiClient();
  const { setPendingDeletes, setLoadingPendingDeletes, decrementPendingDeletesCount } =
    useAdminGamesStore();

  const fetchPendingDeletes = useCallback(async () => {
    setLoadingPendingDeletes(true);
    try {
      const result = await sharedGames.getPendingDeletes({ page: 1, pageSize: 100 });
      setPendingDeletes(result);
    } catch (error) {
      logger.error('Failed to fetch pending deletes:', error);
      // Non-critical for badge display
    }
  }, [sharedGames, setPendingDeletes, setLoadingPendingDeletes]);

  return { fetchPendingDeletes, decrementPendingDeletesCount };
}

/**
 * Hook for fetching a single game by ID
 */
export function useGameDetail() {
  const { sharedGames } = useApiClient();
  const { setSelectedGame, setLoadingSelectedGame, setError } = useAdminGamesStore();

  const fetchGame = useCallback(
    async (id: string) => {
      setLoadingSelectedGame(true);
      try {
        const game = await sharedGames.getById(id);
        setSelectedGame(game);
        return game;
      } catch (error) {
        logger.error('Failed to fetch game:', error);
        setError(error instanceof Error ? error.message : 'Failed to fetch game');
        return null;
      }
    },
    [sharedGames, setSelectedGame, setLoadingSelectedGame, setError]
  );

  const clearGame = useCallback(() => {
    setSelectedGame(null);
  }, [setSelectedGame]);

  return { fetchGame, clearGame };
}

// ============================================================================
// Combined Initialization Hook
// ============================================================================

/**
 * Hook for initializing admin games page data
 * Fetches games, reference data, and pending deletes count
 */
export function useAdminGamesInit() {
  const { fetchGames } = useAdminGamesData();
  const { fetchAll: fetchReferenceData } = useReferenceData();
  const { fetchPendingDeletes } = usePendingDeletesData();

  // Fetch on mount and when page/filters change
  useEffect(() => {
    fetchGames();
  }, [fetchGames]);

  // Fetch reference data once
  useEffect(() => {
    fetchReferenceData();
  }, [fetchReferenceData]);

  // Fetch pending deletes once
  useEffect(() => {
    fetchPendingDeletes();
  }, [fetchPendingDeletes]);

  return { refetchGames: fetchGames };
}
