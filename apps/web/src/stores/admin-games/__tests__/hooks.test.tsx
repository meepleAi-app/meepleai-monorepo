/**
 * Admin Games Hooks Test Suite (Issue #2766)
 *
 * Tests for admin-games store hooks:
 * - Selector hooks (useAdminGames, useAdminGamesFilters, etc.)
 * - Data fetching hooks (useAdminGamesData, useReferenceData, etc.)
 * - Combined initialization hook (useAdminGamesInit)
 *
 * Uses MSW for API mocking
 */

import React from 'react';
import { renderHook, act, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/__tests__/mocks/server';
import { ApiClientProvider } from '@/lib/api/context';
import { createApiClient } from '@/lib/api';
import {
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
  useAdminGamesData,
  useReferenceData,
  usePendingDeletesData,
  useGameDetail,
  useAdminGamesInit,
} from '../hooks';
import { useAdminGamesStore } from '../store';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Test data factories
const createMockSharedGame = (overrides?: Partial<{ id: string; title: string }>) => ({
  id: overrides?.id ?? 'game-1',
  title: overrides?.title ?? 'Test Game',
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
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
});

const createMockCategory = (id: string, name: string) => ({ id, name });
const createMockMechanic = (id: string, name: string) => ({ id, name });

// Wrapper with API client provider
const createWrapper = () => {
  const apiClient = createApiClient();
  return ({ children }: { children: React.ReactNode }) => (
    <ApiClientProvider client={apiClient}>{children}</ApiClientProvider>
  );
};

describe('Admin Games Selector Hooks', () => {
  beforeEach(() => {
    act(() => {
      useAdminGamesStore.getState().reset();
    });
  });

  describe('useAdminGames', () => {
    it('returns games array from store', () => {
      const mockGames = [createMockSharedGame({ id: 'g1' }), createMockSharedGame({ id: 'g2' })];

      act(() => {
        useAdminGamesStore.getState().setGames({
          items: mockGames,
          total: 2,
          page: 1,
          pageSize: 20,
        });
      });

      const { result } = renderHook(() => useAdminGames());
      expect(result.current).toEqual(mockGames);
    });
  });

  describe('useAdminGamesTotal', () => {
    it('returns total count from store', () => {
      act(() => {
        useAdminGamesStore.getState().setGames({
          items: [],
          total: 150,
          page: 1,
          pageSize: 20,
        });
      });

      const { result } = renderHook(() => useAdminGamesTotal());
      expect(result.current).toBe(150);
    });
  });

  describe('useAdminGamesPage', () => {
    it('returns current page from store', () => {
      act(() => {
        useAdminGamesStore.getState().setPage(5);
      });

      const { result } = renderHook(() => useAdminGamesPage());
      expect(result.current).toBe(5);
    });
  });

  describe('useAdminGamesPageSize', () => {
    it('returns page size from store', () => {
      act(() => {
        useAdminGamesStore.getState().setPageSize(50);
      });

      const { result } = renderHook(() => useAdminGamesPageSize());
      expect(result.current).toBe(50);
    });
  });

  describe('useAdminGamesFilters', () => {
    it('returns filters from store', () => {
      act(() => {
        useAdminGamesStore.getState().setFilters({ searchTerm: 'chess' });
      });

      const { result } = renderHook(() => useAdminGamesFilters());
      expect(result.current.searchTerm).toBe('chess');
    });
  });

  describe('useGameCategories', () => {
    it('returns categories from store', () => {
      const categories = [createMockCategory('c1', 'Strategy'), createMockCategory('c2', 'Family')];

      act(() => {
        useAdminGamesStore.getState().setCategories(categories);
      });

      const { result } = renderHook(() => useGameCategories());
      expect(result.current).toEqual(categories);
    });
  });

  describe('useGameMechanics', () => {
    it('returns mechanics from store', () => {
      const mechanics = [
        createMockMechanic('m1', 'Dice Rolling'),
        createMockMechanic('m2', 'Area Control'),
      ];

      act(() => {
        useAdminGamesStore.getState().setMechanics(mechanics);
      });

      const { result } = renderHook(() => useGameMechanics());
      expect(result.current).toEqual(mechanics);
    });
  });

  describe('usePendingDeletesCount', () => {
    it('returns pending deletes count from store', () => {
      act(() => {
        useAdminGamesStore.getState().setPendingDeletes({
          items: [],
          total: 10,
          page: 1,
          pageSize: 100,
        });
      });

      const { result } = renderHook(() => usePendingDeletesCount());
      expect(result.current).toBe(10);
    });
  });

  describe('useSelectedGame', () => {
    it('returns selected game from store', () => {
      const game = {
        id: 'g1',
        title: 'Selected Game',
        description: 'Test',
        status: 1,
        bggId: 123,
        yearPublished: 2024,
        minPlayers: 2,
        maxPlayers: 4,
        playingTime: 60,
        minPlayTime: 45,
        maxPlayTime: 90,
        minAge: 10,
        complexity: 2.5,
        categories: [],
        mechanics: [],
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };

      act(() => {
        useAdminGamesStore.getState().setSelectedGame(game);
      });

      const { result } = renderHook(() => useSelectedGame());
      expect(result.current?.title).toBe('Selected Game');
    });
  });

  describe('useIsLoadingGames', () => {
    it('returns loading state from store', () => {
      act(() => {
        useAdminGamesStore.getState().setLoadingGames(true);
      });

      const { result } = renderHook(() => useIsLoadingGames());
      expect(result.current).toBe(true);
    });
  });

  describe('useIsLoadingReferenceData', () => {
    it('returns true when categories are loading', () => {
      act(() => {
        useAdminGamesStore.getState().setLoadingCategories(true);
      });

      const { result } = renderHook(() => useIsLoadingReferenceData());
      expect(result.current).toBe(true);
    });

    it('returns true when mechanics are loading', () => {
      act(() => {
        useAdminGamesStore.getState().setLoadingMechanics(true);
      });

      const { result } = renderHook(() => useIsLoadingReferenceData());
      expect(result.current).toBe(true);
    });

    it('returns false when neither are loading', () => {
      const { result } = renderHook(() => useIsLoadingReferenceData());
      expect(result.current).toBe(false);
    });
  });

  describe('useTotalPages', () => {
    it('calculates total pages correctly', () => {
      act(() => {
        useAdminGamesStore.getState().setGames({
          items: [],
          total: 100,
          page: 1,
          pageSize: 20,
        });
      });

      const { result } = renderHook(() => useTotalPages());
      expect(result.current).toBe(5);
    });
  });

  describe('useHasActiveFilters', () => {
    it('returns true when searchTerm is set', () => {
      act(() => {
        useAdminGamesStore.getState().setFilters({ searchTerm: 'test' });
      });

      const { result } = renderHook(() => useHasActiveFilters());
      expect(result.current).toBe(true);
    });

    it('returns true when status is set', () => {
      act(() => {
        useAdminGamesStore.getState().setFilters({ status: 1 });
      });

      const { result } = renderHook(() => useHasActiveFilters());
      expect(result.current).toBe(true);
    });

    it('returns false when no filters active', () => {
      const { result } = renderHook(() => useHasActiveFilters());
      expect(result.current).toBe(false);
    });
  });

  describe('useAdminGamesError', () => {
    it('returns error from store', () => {
      act(() => {
        useAdminGamesStore.getState().setError('Test error');
      });

      const { result } = renderHook(() => useAdminGamesError());
      expect(result.current).toBe('Test error');
    });
  });
});

describe('Admin Games Data Fetching Hooks', () => {
  beforeEach(() => {
    act(() => {
      useAdminGamesStore.getState().reset();
    });

    // Set up default handlers
    server.use(
      http.get(`${API_BASE}/api/v1/admin/shared-games`, () => {
        return HttpResponse.json({
          items: [createMockSharedGame()],
          total: 1,
          page: 1,
          pageSize: 20,
        });
      }),
      http.get(`${API_BASE}/api/v1/shared-games/categories`, () => {
        return HttpResponse.json([createMockCategory('c1', 'Strategy')]);
      }),
      http.get(`${API_BASE}/api/v1/shared-games/mechanics`, () => {
        return HttpResponse.json([createMockMechanic('m1', 'Dice Rolling')]);
      }),
      http.get(`${API_BASE}/api/v1/admin/shared-games/delete-requests`, () => {
        return HttpResponse.json({
          items: [],
          total: 0,
          page: 1,
          pageSize: 100,
        });
      }),
      http.get(`${API_BASE}/api/v1/shared-games/:id`, () => {
        return HttpResponse.json({
          id: 'game-1',
          title: 'Detailed Game',
          description: 'Test',
          status: 1,
          bggId: 123,
          yearPublished: 2024,
          minPlayers: 2,
          maxPlayers: 4,
          playingTime: 60,
          minPlayTime: 45,
          maxPlayTime: 90,
          minAge: 10,
          complexity: 2.5,
          categories: [],
          mechanics: [],
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        });
      })
    );
  });

  describe('useAdminGamesData', () => {
    it('provides fetchGames function', () => {
      const { result } = renderHook(() => useAdminGamesData(), { wrapper: createWrapper() });
      expect(typeof result.current.fetchGames).toBe('function');
    });
  });

  describe('useReferenceData', () => {
    it('provides fetch functions', () => {
      const { result } = renderHook(() => useReferenceData(), { wrapper: createWrapper() });

      expect(typeof result.current.fetchCategories).toBe('function');
      expect(typeof result.current.fetchMechanics).toBe('function');
      expect(typeof result.current.fetchAll).toBe('function');
    });

    it('fetchCategories does not fetch if already loaded', async () => {
      // Pre-populate categories
      act(() => {
        useAdminGamesStore.getState().setCategories([createMockCategory('c1', 'Strategy')]);
      });

      let fetchCalled = false;
      server.use(
        http.get(`${API_BASE}/api/v1/shared-games/categories`, () => {
          fetchCalled = true;
          return HttpResponse.json([createMockCategory('c2', 'Party')]);
        })
      );

      const { result } = renderHook(() => useReferenceData(), { wrapper: createWrapper() });

      await act(async () => {
        await result.current.fetchCategories();
      });

      expect(fetchCalled).toBe(false);
    });
  });

  describe('usePendingDeletesData', () => {
    it('provides fetch and decrement functions', () => {
      const { result } = renderHook(() => usePendingDeletesData(), { wrapper: createWrapper() });

      expect(typeof result.current.fetchPendingDeletes).toBe('function');
      expect(typeof result.current.decrementPendingDeletesCount).toBe('function');
    });
  });

  describe('useGameDetail', () => {
    it('provides fetchGame and clearGame functions', () => {
      const { result } = renderHook(() => useGameDetail(), { wrapper: createWrapper() });

      expect(typeof result.current.fetchGame).toBe('function');
      expect(typeof result.current.clearGame).toBe('function');
    });

    it('clearGame sets selected game to null', () => {
      // Pre-set a selected game
      act(() => {
        useAdminGamesStore.getState().setSelectedGame({
          id: 'g1',
          title: 'Test',
          description: 'Test',
          status: 1,
          bggId: 123,
          yearPublished: 2024,
          minPlayers: 2,
          maxPlayers: 4,
          playingTime: 60,
          minPlayTime: 45,
          maxPlayTime: 90,
          minAge: 10,
          complexity: 2.5,
          categories: [],
          mechanics: [],
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        });
      });

      const { result } = renderHook(() => useGameDetail(), { wrapper: createWrapper() });

      act(() => {
        result.current.clearGame();
      });

      expect(useAdminGamesStore.getState().selectedGame).toBeNull();
    });
  });

  describe('useAdminGamesInit', () => {
    it('provides refetchGames function', () => {
      const { result } = renderHook(() => useAdminGamesInit(), { wrapper: createWrapper() });

      expect(typeof result.current.refetchGames).toBe('function');
    });
  });
});
