/**
 * Admin Games Store Test Suite (Issue #2766)
 *
 * Tests for useAdminGamesStore Zustand store:
 * - Initial state
 * - Games actions (setGames, setPage, setPageSize, setFilters, resetFilters)
 * - Reference data actions (setCategories, setMechanics)
 * - Pending deletes actions
 * - Selected game actions
 * - Loading state actions
 * - Error state actions
 * - Reset action
 * - Selectors
 */

import { act } from '@testing-library/react';
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
  selectPendingDeleteRequests,
  selectSelectedGame,
  selectIsLoadingGames,
  selectIsLoadingAnyReferenceData,
  selectTotalPages,
  selectHasActiveFilters,
  selectError,
  type AdminGamesState,
  type AdminGamesFilters,
} from '../store';

// Test data factories
const createMockSharedGame = (overrides?: Partial<{ id: string; title: string; status: number }>) => ({
  id: overrides?.id ?? 'game-1',
  title: overrides?.title ?? 'Test Game',
  status: overrides?.status ?? 1,
  bggId: 12345,
  yearPublished: 2024,
  minPlayers: 2,
  maxPlayers: 4,
  playingTime: 60,
  minPlayTime: 45,
  maxPlayTime: 90,
  minAge: 10,
  complexity: 2.5,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
});

const createMockCategory = (overrides?: Partial<{ id: string; name: string }>) => ({
  id: overrides?.id ?? 'cat-1',
  name: overrides?.name ?? 'Strategy',
});

const createMockMechanic = (overrides?: Partial<{ id: string; name: string }>) => ({
  id: overrides?.id ?? 'mech-1',
  name: overrides?.name ?? 'Dice Rolling',
});

const createMockDeleteRequest = (overrides?: Partial<{ id: string; gameId: string }>) => ({
  id: overrides?.id ?? 'del-1',
  gameId: overrides?.gameId ?? 'game-1',
  reason: 'Duplicate entry',
  requestedAt: new Date().toISOString(),
  requestedByUserId: 'user-1',
});

const createMockGameDetail = () => ({
  id: 'game-1',
  title: 'Detailed Game',
  description: 'A detailed description',
  status: 1,
  bggId: 12345,
  yearPublished: 2024,
  minPlayers: 2,
  maxPlayers: 4,
  playingTime: 60,
  minPlayTime: 45,
  maxPlayTime: 90,
  minAge: 10,
  complexity: 2.5,
  categories: [createMockCategory()],
  mechanics: [createMockMechanic()],
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
});

describe('useAdminGamesStore', () => {
  // Reset store before each test
  beforeEach(() => {
    act(() => {
      useAdminGamesStore.getState().reset();
    });
  });

  describe('Initial State', () => {
    it('has correct default values', () => {
      const state = useAdminGamesStore.getState();

      expect(state.games).toEqual([]);
      expect(state.total).toBe(0);
      expect(state.page).toBe(1);
      expect(state.pageSize).toBe(20);
      expect(state.filters).toEqual({
        searchTerm: '',
        status: null,
        categoryIds: null,
        mechanicIds: null,
        sortBy: 'title',
        sortDescending: false,
      });
      expect(state.categories).toEqual([]);
      expect(state.mechanics).toEqual([]);
      expect(state.pendingDeleteRequests).toEqual([]);
      expect(state.pendingDeletesCount).toBe(0);
      expect(state.selectedGame).toBeNull();
      expect(state.isLoadingGames).toBe(false);
      expect(state.isLoadingCategories).toBe(false);
      expect(state.isLoadingMechanics).toBe(false);
      expect(state.isLoadingPendingDeletes).toBe(false);
      expect(state.isLoadingSelectedGame).toBe(false);
      expect(state.error).toBeNull();
    });
  });

  describe('Games Actions', () => {
    it('setGames updates games list and pagination', () => {
      const mockGames = [
        createMockSharedGame({ id: 'g1', title: 'Game 1' }),
        createMockSharedGame({ id: 'g2', title: 'Game 2' }),
      ];

      act(() => {
        useAdminGamesStore.getState().setLoadingGames(true);
        useAdminGamesStore.getState().setGames({
          items: mockGames,
          total: 50,
          page: 2,
          pageSize: 10,
        });
      });

      const state = useAdminGamesStore.getState();
      expect(state.games).toEqual(mockGames);
      expect(state.total).toBe(50);
      expect(state.page).toBe(2);
      expect(state.pageSize).toBe(10);
      expect(state.isLoadingGames).toBe(false);
    });

    it('setPage updates current page', () => {
      act(() => {
        useAdminGamesStore.getState().setPage(5);
      });

      expect(useAdminGamesStore.getState().page).toBe(5);
    });

    it('setPageSize updates page size and resets to page 1', () => {
      act(() => {
        useAdminGamesStore.getState().setPage(3);
        useAdminGamesStore.getState().setPageSize(50);
      });

      const state = useAdminGamesStore.getState();
      expect(state.pageSize).toBe(50);
      expect(state.page).toBe(1);
    });

    it('setFilters updates filters and resets to page 1', () => {
      act(() => {
        useAdminGamesStore.getState().setPage(5);
        useAdminGamesStore.getState().setFilters({
          searchTerm: 'chess',
          status: 1,
        });
      });

      const state = useAdminGamesStore.getState();
      expect(state.filters.searchTerm).toBe('chess');
      expect(state.filters.status).toBe(1);
      expect(state.page).toBe(1);
    });

    it('setFilters preserves unmodified filter values', () => {
      act(() => {
        useAdminGamesStore.getState().setFilters({
          searchTerm: 'test',
        });
      });

      const state = useAdminGamesStore.getState();
      expect(state.filters.searchTerm).toBe('test');
      expect(state.filters.sortBy).toBe('title'); // preserved default
    });

    it('resetFilters restores default filters and page 1', () => {
      act(() => {
        useAdminGamesStore.getState().setFilters({
          searchTerm: 'test',
          status: 2,
          categoryIds: 'cat-1',
          mechanicIds: 'mech-1',
          sortBy: 'complexity',
          sortDescending: true,
        });
        useAdminGamesStore.getState().setPage(10);
        useAdminGamesStore.getState().resetFilters();
      });

      const state = useAdminGamesStore.getState();
      expect(state.filters).toEqual({
        searchTerm: '',
        status: null,
        categoryIds: null,
        mechanicIds: null,
        sortBy: 'title',
        sortDescending: false,
      });
      expect(state.page).toBe(1);
    });
  });

  describe('Reference Data Actions', () => {
    it('setCategories updates categories and clears loading', () => {
      const categories = [
        createMockCategory({ id: 'c1', name: 'Strategy' }),
        createMockCategory({ id: 'c2', name: 'Party' }),
      ];

      act(() => {
        useAdminGamesStore.getState().setLoadingCategories(true);
        useAdminGamesStore.getState().setCategories(categories);
      });

      const state = useAdminGamesStore.getState();
      expect(state.categories).toEqual(categories);
      expect(state.isLoadingCategories).toBe(false);
    });

    it('setMechanics updates mechanics and clears loading', () => {
      const mechanics = [
        createMockMechanic({ id: 'm1', name: 'Dice Rolling' }),
        createMockMechanic({ id: 'm2', name: 'Worker Placement' }),
      ];

      act(() => {
        useAdminGamesStore.getState().setLoadingMechanics(true);
        useAdminGamesStore.getState().setMechanics(mechanics);
      });

      const state = useAdminGamesStore.getState();
      expect(state.mechanics).toEqual(mechanics);
      expect(state.isLoadingMechanics).toBe(false);
    });
  });

  describe('Pending Deletes Actions', () => {
    it('setPendingDeletes updates requests and count', () => {
      const requests = [
        createMockDeleteRequest({ id: 'd1' }),
        createMockDeleteRequest({ id: 'd2' }),
      ];

      act(() => {
        useAdminGamesStore.getState().setLoadingPendingDeletes(true);
        useAdminGamesStore.getState().setPendingDeletes({
          items: requests,
          total: 5,
          page: 1,
          pageSize: 100,
        });
      });

      const state = useAdminGamesStore.getState();
      expect(state.pendingDeleteRequests).toEqual(requests);
      expect(state.pendingDeletesCount).toBe(5);
      expect(state.isLoadingPendingDeletes).toBe(false);
    });

    it('decrementPendingDeletesCount decreases count by 1', () => {
      act(() => {
        useAdminGamesStore.getState().setPendingDeletes({
          items: [],
          total: 10,
          page: 1,
          pageSize: 100,
        });
        useAdminGamesStore.getState().decrementPendingDeletesCount();
      });

      expect(useAdminGamesStore.getState().pendingDeletesCount).toBe(9);
    });

    it('decrementPendingDeletesCount does not go below 0', () => {
      act(() => {
        useAdminGamesStore.getState().setPendingDeletes({
          items: [],
          total: 0,
          page: 1,
          pageSize: 100,
        });
        useAdminGamesStore.getState().decrementPendingDeletesCount();
      });

      expect(useAdminGamesStore.getState().pendingDeletesCount).toBe(0);
    });
  });

  describe('Selected Game Actions', () => {
    it('setSelectedGame updates selected game and clears loading', () => {
      const game = createMockGameDetail();

      act(() => {
        useAdminGamesStore.getState().setLoadingSelectedGame(true);
        useAdminGamesStore.getState().setSelectedGame(game);
      });

      const state = useAdminGamesStore.getState();
      expect(state.selectedGame).toEqual(game);
      expect(state.isLoadingSelectedGame).toBe(false);
    });

    it('setSelectedGame can set to null', () => {
      const game = createMockGameDetail();

      act(() => {
        useAdminGamesStore.getState().setSelectedGame(game);
        useAdminGamesStore.getState().setSelectedGame(null);
      });

      expect(useAdminGamesStore.getState().selectedGame).toBeNull();
    });
  });

  describe('Loading Actions', () => {
    it('setLoadingGames updates loading state and clears error', () => {
      act(() => {
        useAdminGamesStore.getState().setError('Previous error');
        useAdminGamesStore.getState().setLoadingGames(true);
      });

      const state = useAdminGamesStore.getState();
      expect(state.isLoadingGames).toBe(true);
      expect(state.error).toBeNull();
    });

    it('setLoadingCategories updates loading state', () => {
      act(() => {
        useAdminGamesStore.getState().setLoadingCategories(true);
      });

      expect(useAdminGamesStore.getState().isLoadingCategories).toBe(true);
    });

    it('setLoadingMechanics updates loading state', () => {
      act(() => {
        useAdminGamesStore.getState().setLoadingMechanics(true);
      });

      expect(useAdminGamesStore.getState().isLoadingMechanics).toBe(true);
    });

    it('setLoadingPendingDeletes updates loading state', () => {
      act(() => {
        useAdminGamesStore.getState().setLoadingPendingDeletes(true);
      });

      expect(useAdminGamesStore.getState().isLoadingPendingDeletes).toBe(true);
    });

    it('setLoadingSelectedGame updates loading and clears selected game', () => {
      const game = createMockGameDetail();

      act(() => {
        useAdminGamesStore.getState().setSelectedGame(game);
        useAdminGamesStore.getState().setLoadingSelectedGame(true);
      });

      const state = useAdminGamesStore.getState();
      expect(state.isLoadingSelectedGame).toBe(true);
      expect(state.selectedGame).toBeNull();
    });
  });

  describe('Error Actions', () => {
    it('setError updates error and clears loading states', () => {
      act(() => {
        useAdminGamesStore.getState().setLoadingGames(true);
        useAdminGamesStore.getState().setLoadingSelectedGame(true);
        useAdminGamesStore.getState().setError('Something went wrong');
      });

      const state = useAdminGamesStore.getState();
      expect(state.error).toBe('Something went wrong');
      expect(state.isLoadingGames).toBe(false);
      expect(state.isLoadingSelectedGame).toBe(false);
    });

    it('setError can set to null', () => {
      act(() => {
        useAdminGamesStore.getState().setError('Error');
        useAdminGamesStore.getState().setError(null);
      });

      expect(useAdminGamesStore.getState().error).toBeNull();
    });

    it('clearError clears error', () => {
      act(() => {
        useAdminGamesStore.getState().setError('Error');
        useAdminGamesStore.getState().clearError();
      });

      expect(useAdminGamesStore.getState().error).toBeNull();
    });
  });

  describe('Reset Action', () => {
    it('reset restores all state to initial values', () => {
      // Populate state
      act(() => {
        useAdminGamesStore.getState().setGames({
          items: [createMockSharedGame()],
          total: 100,
          page: 5,
          pageSize: 50,
        });
        useAdminGamesStore.getState().setFilters({ searchTerm: 'test' });
        useAdminGamesStore.getState().setCategories([createMockCategory()]);
        useAdminGamesStore.getState().setMechanics([createMockMechanic()]);
        useAdminGamesStore.getState().setPendingDeletes({
          items: [createMockDeleteRequest()],
          total: 10,
          page: 1,
          pageSize: 100,
        });
        useAdminGamesStore.getState().setSelectedGame(createMockGameDetail());
        useAdminGamesStore.getState().setError('Error');
      });

      // Reset
      act(() => {
        useAdminGamesStore.getState().reset();
      });

      const state = useAdminGamesStore.getState();
      expect(state.games).toEqual([]);
      expect(state.total).toBe(0);
      expect(state.page).toBe(1);
      expect(state.pageSize).toBe(20);
      expect(state.filters.searchTerm).toBe('');
      expect(state.categories).toEqual([]);
      expect(state.mechanics).toEqual([]);
      expect(state.pendingDeleteRequests).toEqual([]);
      expect(state.pendingDeletesCount).toBe(0);
      expect(state.selectedGame).toBeNull();
      expect(state.error).toBeNull();
    });
  });

  describe('Selectors', () => {
    beforeEach(() => {
      // Set up some state for selector tests
      // NOTE: setFilters resets page to 1, so call it BEFORE setGames
      // to ensure page:3 is preserved
      act(() => {
        useAdminGamesStore.getState().setFilters({ searchTerm: 'test' });
        useAdminGamesStore.getState().setGames({
          items: [createMockSharedGame()],
          total: 100,
          page: 3,
          pageSize: 20,
        });
        useAdminGamesStore.getState().setCategories([createMockCategory()]);
        useAdminGamesStore.getState().setMechanics([createMockMechanic()]);
        useAdminGamesStore.getState().setPendingDeletes({
          items: [createMockDeleteRequest()],
          total: 5,
          page: 1,
          pageSize: 100,
        });
        useAdminGamesStore.getState().setSelectedGame(createMockGameDetail());
        useAdminGamesStore.getState().setLoadingCategories(true);
        useAdminGamesStore.getState().setError('Test error');
      });
    });

    it('selectGames returns games array', () => {
      const games = selectGames(useAdminGamesStore.getState());
      expect(games).toHaveLength(1);
    });

    it('selectTotal returns total count', () => {
      const total = selectTotal(useAdminGamesStore.getState());
      expect(total).toBe(100);
    });

    it('selectPage returns current page', () => {
      const page = selectPage(useAdminGamesStore.getState());
      expect(page).toBe(3);
    });

    it('selectPageSize returns page size', () => {
      const pageSize = selectPageSize(useAdminGamesStore.getState());
      expect(pageSize).toBe(20);
    });

    it('selectFilters returns filters object', () => {
      const filters = selectFilters(useAdminGamesStore.getState());
      expect(filters.searchTerm).toBe('test');
    });

    it('selectCategories returns categories array', () => {
      const categories = selectCategories(useAdminGamesStore.getState());
      expect(categories).toHaveLength(1);
    });

    it('selectMechanics returns mechanics array', () => {
      const mechanics = selectMechanics(useAdminGamesStore.getState());
      expect(mechanics).toHaveLength(1);
    });

    it('selectPendingDeletesCount returns count', () => {
      const count = selectPendingDeletesCount(useAdminGamesStore.getState());
      expect(count).toBe(5);
    });

    it('selectPendingDeleteRequests returns requests array', () => {
      const requests = selectPendingDeleteRequests(useAdminGamesStore.getState());
      expect(requests).toHaveLength(1);
    });

    it('selectSelectedGame returns selected game', () => {
      const game = selectSelectedGame(useAdminGamesStore.getState());
      expect(game?.title).toBe('Detailed Game');
    });

    it('selectIsLoadingGames returns loading state', () => {
      act(() => {
        useAdminGamesStore.getState().setLoadingGames(true);
      });
      const isLoading = selectIsLoadingGames(useAdminGamesStore.getState());
      expect(isLoading).toBe(true);
    });

    it('selectIsLoadingAnyReferenceData returns true if categories or mechanics loading', () => {
      const isLoading = selectIsLoadingAnyReferenceData(useAdminGamesStore.getState());
      expect(isLoading).toBe(true);
    });

    it('selectError returns error string', () => {
      const error = selectError(useAdminGamesStore.getState());
      expect(error).toBe('Test error');
    });

    it('selectTotalPages calculates correct page count', () => {
      const totalPages = selectTotalPages(useAdminGamesStore.getState());
      expect(totalPages).toBe(5); // 100 / 20 = 5
    });

    it('selectHasActiveFilters returns true when filters are active', () => {
      const hasFilters = selectHasActiveFilters(useAdminGamesStore.getState());
      expect(hasFilters).toBe(true);
    });

    it('selectHasActiveFilters returns false when no filters active', () => {
      act(() => {
        useAdminGamesStore.getState().resetFilters();
      });
      const hasFilters = selectHasActiveFilters(useAdminGamesStore.getState());
      expect(hasFilters).toBe(false);
    });
  });
});
