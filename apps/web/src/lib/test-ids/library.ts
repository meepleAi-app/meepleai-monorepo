/**
 * Test ID constants for Library-related components.
 *
 * Covers: PrivateGamesClient, AddPrivateGameWithBgg
 *
 * Import from '@/lib/test-ids' in both components and tests.
 */

export const LIBRARY_TEST_IDS = {
  // PrivateGamesClient
  loadingState: 'loading-state',
  addPrivateGameBtn: 'add-private-game-btn',
  searchInput: 'search-input',
  errorState: 'error-state',
  gamesGrid: 'games-grid',
  gameCard: (id: string) => `game-card-${id}` as const,
  editBtn: (id: string) => `edit-btn-${id}` as const,
  deleteBtn: (id: string) => `delete-btn-${id}` as const,
  emptyState: 'empty-state',
  sortDirectionBtn: 'sort-direction-btn',
  pagination: 'pagination',
  confirmDeleteBtn: 'confirm-delete-btn',

  // AddPrivateGameWithBgg
  addGameChooseMode: 'add-game-choose-mode',
  manualEntryBtn: 'manual-entry-btn',
  addGameManualForm: 'add-game-manual-form',
  addGameBggForm: 'add-game-bgg-form',
  loadingBggDetails: 'loading-bgg-details',
  backToSearchBtn: 'back-to-search-btn',
} as const;
