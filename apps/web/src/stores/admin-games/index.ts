/**
 * Admin Games Store Module (Issue #2372)
 *
 * Centralized exports for Zustand-based admin games state management.
 *
 * Usage:
 *   import { useAdminGamesStore, useAdminGames, useAdminGamesInit } from '@/stores/admin-games';
 *
 * Features:
 * - Games list with pagination and filters
 * - Reference data (categories, mechanics)
 * - Pending delete requests tracking
 * - Selected game for editing
 */

// Main store
export { useAdminGamesStore, type AdminGamesState, type AdminGamesFilters } from './store';

// Selectors
export {
  selectGames,
  selectTotal,
  selectPage,
  selectPageSize,
  selectFilters,
  selectCategories,
  selectMechanics,
  selectPendingDeletesCount,
  selectPendingDeleteRequests,
  selectSelectedGame,
  selectIsLoadingGames,
  selectIsLoadingAnyReferenceData,
  selectError,
  selectTotalPages,
  selectHasActiveFilters,
} from './store';

// Hooks with optimized selectors
export {
  useAdminGames,
  useAdminGamesTotal,
  useAdminGamesPage,
  useAdminGamesPageSize,
  useAdminGamesFilters,
  useGameCategories,
  useGameMechanics,
  usePendingDeletesCount,
  useSelectedGame,
  useIsLoadingGames,
  useIsLoadingReferenceData,
  useTotalPages,
  useHasActiveFilters,
  useAdminGamesError,
} from './hooks';

// Data fetching hooks
export {
  useAdminGamesData,
  useReferenceData,
  usePendingDeletesData,
  useGameDetail,
  useAdminGamesInit,
} from './hooks';
