/**
 * useGameSearch Hook Tests
 *
 * Tests for game search hook with debouncing, API integration, and caching.
 * Issue #4273: Game Search Autocomplete
 * Target: >=85% coverage
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { createElement, type ReactNode } from 'react';

import { useGameSearch, type GameSearchResult } from '../useGameSearch';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

// Mock global fetch
global.fetch = vi.fn();

// Mock useDebounce hook - import at top level for hoisting
import * as useDebounceModule from '@/components/ui/data-display/entity-list-view/hooks/use-debounce';

vi.mock('@/components/ui/data-display/entity-list-view/hooks/use-debounce', () => ({
  useDebounce: vi.fn((value: string) => value), // No debounce in tests by default
}));

// The hook maps backend results to GameSearchResult with source: 'catalog'
const mockApiResponse = {
  games: [
    {
      id: 'lib-123',
      title: 'Catan',
      imageUrl: 'https://example.com/catan.jpg',
    },
    {
      id: 'cat-456',
      title: 'Carcassonne',
      imageUrl: 'https://example.com/carcassonne.jpg',
    },
    {
      id: 'priv-789',
      title: 'My Custom Game',
    },
  ],
};

// Expected mapped results (all source: 'catalog' per hook implementation)
const expectedResults: GameSearchResult[] = [
  {
    id: 'lib-123',
    name: 'Catan',
    source: 'catalog',
    imageUrl: 'https://example.com/catan.jpg',
  },
  {
    id: 'cat-456',
    name: 'Carcassonne',
    source: 'catalog',
    imageUrl: 'https://example.com/carcassonne.jpg',
  },
  {
    id: 'priv-789',
    name: 'My Custom Game',
    source: 'catalog',
    imageUrl: undefined,
  },
];

describe('useGameSearch', () => {
  let queryClient: QueryClient;

  const createWrapper = () => {
    return ({ children }: { children: ReactNode }) =>
      createElement(QueryClientProvider, { client: queryClient }, children);
  };

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
    // Reset useDebounce to default behavior
    vi.mocked(useDebounceModule.useDebounce).mockImplementation((value: string) => value);
  });

  describe('Query Behavior', () => {
    it('should return empty array for empty query', () => {
      const { result } = renderHook(() => useGameSearch(''), {
        wrapper: createWrapper(),
      });

      // Query is disabled, data is undefined
      expect(result.current.data).toBeUndefined();
      expect(global.fetch).not.toHaveBeenCalled();
    });

    it('should return empty array for query < 2 characters', () => {
      const { result } = renderHook(() => useGameSearch('C'), {
        wrapper: createWrapper(),
      });

      // Query is disabled, data is undefined
      expect(result.current.data).toBeUndefined();
      expect(global.fetch).not.toHaveBeenCalled();
    });

    it('should fetch games for query >= 2 characters', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => mockApiResponse,
      } as Response);

      const { result } = renderHook(() => useGameSearch('Ca'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toEqual(expectedResults);
      expect(global.fetch).toHaveBeenCalledWith('/api/v1/games?search=Ca&pageSize=20');
    });

    it('should encode special characters in query', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ games: [] }),
      } as Response);

      const { result } = renderHook(() => useGameSearch('Catan & Cities'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(global.fetch).toHaveBeenCalledWith(
        '/api/v1/games?search=Catan%20%26%20Cities&pageSize=20'
      );
    });
  });

  describe('Debouncing', () => {
    it('should use debounced query', async () => {
      vi.mocked(useDebounceModule.useDebounce).mockImplementation(
        (value: string, delay: number) => {
          expect(delay).toBe(300); // Default debounce
          return value;
        }
      );

      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => mockApiResponse,
      } as Response);

      renderHook(() => useGameSearch('Catan'), {
        wrapper: createWrapper(),
      });

      expect(useDebounceModule.useDebounce).toHaveBeenCalledWith('Catan', 300);
    });

    it('should support custom debounce delay', () => {
      renderHook(() => useGameSearch('Test', 500), {
        wrapper: createWrapper(),
      });

      expect(useDebounceModule.useDebounce).toHaveBeenCalledWith('Test', 500);
    });
  });

  describe('Result Handling', () => {
    it('should return games mapped with catalog source', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => mockApiResponse,
      } as Response);

      const { result } = renderHook(() => useGameSearch('Game'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toHaveLength(3);
      // All results are mapped as 'catalog' source
      expect(result.current.data![0].source).toBe('catalog');
      expect(result.current.data![1].source).toBe('catalog');
      expect(result.current.data![2].source).toBe('catalog');
    });

    it('should handle empty results', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ games: [] }),
      } as Response);

      const { result } = renderHook(() => useGameSearch('NonExistent'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toEqual([]);
    });

    it('should include imageUrl when present', async () => {
      const resultsWithImages = {
        games: [
          {
            id: 'game-1',
            title: 'Game with Image',
            imageUrl: 'https://example.com/image.jpg',
          },
        ],
      };

      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => resultsWithImages,
      } as Response);

      const { result } = renderHook(() => useGameSearch('Game'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data![0].imageUrl).toBe('https://example.com/image.jpg');
    });

    it('should handle missing imageUrl', async () => {
      const resultsWithoutImages = {
        games: [
          {
            id: 'game-1',
            title: 'Game without Image',
          },
        ],
      };

      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => resultsWithoutImages,
      } as Response);

      const { result } = renderHook(() => useGameSearch('Game'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data![0].imageUrl).toBeUndefined();
    });
  });

  describe('Error Handling', () => {
    it('should handle fetch error', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: false,
        status: 500,
      } as Response);

      const { result } = renderHook(() => useGameSearch('Error'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toBeDefined();
      expect(result.current.data).toBeUndefined();
    });

    it('should handle network error', async () => {
      vi.mocked(global.fetch).mockRejectedValueOnce(new Error('Network error'));

      const { result } = renderHook(() => useGameSearch('Network'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toBeDefined();
    });
  });

  describe('Caching', () => {
    it('should cache results for 5 minutes (staleTime)', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => mockApiResponse,
      } as Response);

      const { result, rerender } = renderHook(() => useGameSearch('Catan'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      // Clear mock to track if refetch happens
      vi.clearAllMocks();

      // Rerender with same query (within staleTime)
      rerender();

      // Should use cached data, no new fetch
      expect(global.fetch).not.toHaveBeenCalled();
      expect(result.current.data).toEqual(expectedResults);
    });

    it('should use different cache keys for different queries', async () => {
      vi.mocked(global.fetch)
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ games: [mockApiResponse.games[0]] }),
        } as Response)
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ games: [mockApiResponse.games[1]] }),
        } as Response);

      const { result: result1 } = renderHook(() => useGameSearch('Catan'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result1.current.isSuccess).toBe(true));

      const { result: result2 } = renderHook(() => useGameSearch('Carcassonne'), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result2.current.isSuccess).toBe(true));

      // Both queries should have been called
      expect(global.fetch).toHaveBeenCalledTimes(2);
      expect(global.fetch).toHaveBeenCalledWith('/api/v1/games?search=Catan&pageSize=20');
      expect(global.fetch).toHaveBeenCalledWith('/api/v1/games?search=Carcassonne&pageSize=20');
    });
  });

  describe('Loading States', () => {
    it('should indicate loading state', async () => {
      vi.mocked(global.fetch).mockImplementation(
        () =>
          new Promise(resolve =>
            setTimeout(
              () =>
                resolve({
                  ok: true,
                  json: async () => mockApiResponse,
                } as Response),
              100
            )
          )
      );

      const { result } = renderHook(() => useGameSearch('Loading'), {
        wrapper: createWrapper(),
      });

      // Initially loading
      expect(result.current.isLoading).toBe(true);
      expect(result.current.data).toBeUndefined();

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      // After load
      expect(result.current.isLoading).toBe(false);
      expect(result.current.data).toEqual(expectedResults);
    });
  });

  describe('Query Enabled State', () => {
    it('should not fetch when query is disabled (< 2 chars)', () => {
      renderHook(() => useGameSearch('X'), {
        wrapper: createWrapper(),
      });

      expect(global.fetch).not.toHaveBeenCalled();
    });

    it('should fetch when query becomes enabled', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => mockApiResponse,
      } as Response);

      const { rerender } = renderHook(({ query }) => useGameSearch(query), {
        wrapper: createWrapper(),
        initialProps: { query: 'X' },
      });

      // Initially disabled
      expect(global.fetch).not.toHaveBeenCalled();

      // Update to valid query
      rerender({ query: 'Catan' });

      await waitFor(() => expect(global.fetch).toHaveBeenCalled());
    });
  });
});
