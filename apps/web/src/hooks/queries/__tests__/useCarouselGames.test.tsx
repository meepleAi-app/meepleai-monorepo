/**
 * Tests for useCarouselGames hooks
 *
 * Issue #3590: GC-005 — Unit & Integration Tests
 * Epic: #3585 — GameCarousel Integration & Production Readiness
 *
 * Tests TanStack Query hooks for carousel game data.
 */

import type { Mock } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useCarouselGames,
  useFeaturedGames,
  useTrendingGames,
  useCategoryGames,
  useUserLibraryGames,
  carouselGamesKeys,
  type CarouselSource,
} from '../useCarouselGames';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      search: vi.fn(),
    },
    library: {
      getLibrary: vi.fn(),
    },
  },
}));

describe('useCarouselGames hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  // ==================== Mock Data ====================

  // Mock data matching SharedGame schema
  const mockSharedGamesResponse = {
    items: [
      {
        id: 'shared-1',
        title: 'Gloomhaven',
        yearPublished: 2017,
        minPlayers: 1,
        maxPlayers: 4,
        playingTimeMinutes: 120,
        complexity: 3.86,
        averageRating: 8.7,
        thumbnailUrl: 'https://example.com/gloomhaven-thumb.jpg',
        imageUrl: 'https://example.com/gloomhaven.jpg',
        status: 2,
      },
      {
        id: 'shared-2',
        title: 'Brass: Birmingham',
        yearPublished: 2018,
        minPlayers: 2,
        maxPlayers: 4,
        playingTimeMinutes: 120,
        complexity: 3.92,
        averageRating: 8.6,
        thumbnailUrl: 'https://example.com/brass-thumb.jpg',
        imageUrl: 'https://example.com/brass.jpg',
        status: 2,
      },
    ],
    total: 2,
    page: 1,
    pageSize: 10,
    totalPages: 1,
  };

  // Mock data matching UserLibraryEntry schema
  const mockLibraryResponse = {
    items: [
      {
        id: 'lib-1',
        gameId: 'game-1',
        gameTitle: 'My Game',
        gamePublisher: 'Publisher A',
        gameImageUrl: 'https://example.com/mygame.jpg',
        gameIconUrl: 'https://example.com/mygame-icon.jpg',
        gameYearPublished: 2020,
        isFavorite: true,
        addedAt: '2024-01-15T10:00:00Z',
        notes: 'Great game!',
      },
    ],
    total: 1,
    page: 1,
    pageSize: 10,
    totalPages: 1,
  };

  // ==================== Query Keys ====================

  describe('carouselGamesKeys', () => {
    it('generates correct base query keys', () => {
      expect(carouselGamesKeys.all).toEqual(['carouselGames']);
      expect(carouselGamesKeys.lists()).toEqual(['carouselGames', 'list']);
    });

    it('generates correct list query keys with options', () => {
      expect(carouselGamesKeys.list({ source: 'featured', limit: 10 })).toEqual([
        'carouselGames',
        'list',
        { options: { source: 'featured', limit: 10 } },
      ]);
      expect(carouselGamesKeys.list({ source: 'trending', limit: 10 })).toEqual([
        'carouselGames',
        'list',
        { options: { source: 'trending', limit: 10 } },
      ]);
    });

    it('generates correct list query keys with sort', () => {
      expect(carouselGamesKeys.list({ source: 'featured', limit: 10, sort: 'rating' })).toEqual([
        'carouselGames',
        'list',
        { options: { source: 'featured', limit: 10, sort: 'rating' } },
      ]);
      expect(carouselGamesKeys.list({ source: 'trending', limit: 10, sort: 'popularity' })).toEqual([
        'carouselGames',
        'list',
        { options: { source: 'trending', limit: 10, sort: 'popularity' } },
      ]);
    });

    it('generates unique keys for different sources', () => {
      const keyFeatured = carouselGamesKeys.list('featured');
      const keyTrending = carouselGamesKeys.list('trending');
      const keyCategory = carouselGamesKeys.list('category');
      const keyLibrary = carouselGamesKeys.list('user-library');

      expect(keyFeatured).not.toEqual(keyTrending);
      expect(keyTrending).not.toEqual(keyCategory);
      expect(keyCategory).not.toEqual(keyLibrary);
    });
  });

  // ==================== useCarouselGames ====================

  describe('useCarouselGames', () => {
    it('fetches featured games successfully', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockSharedGamesResponse);

      const { result } = renderHook(
        () => useCarouselGames({ source: 'featured', limit: 10 }),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalled();
      expect(result.current.data?.games).toHaveLength(2);
      expect(result.current.data?.games[0].title).toBe('Gloomhaven');
    });

    it('fetches trending games successfully', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockSharedGamesResponse);

      const { result } = renderHook(
        () => useCarouselGames({ source: 'trending', limit: 10 }),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalled();
      expect(result.current.data?.games).toHaveLength(2);
    });

    it('fetches category games with categoryId parameter', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockSharedGamesResponse);

      const { result } = renderHook(
        () =>
          useCarouselGames({
            source: 'category',
            categoryId: 'strategy',
            limit: 10,
          }),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(
        expect.objectContaining({
          categoryIds: 'strategy',
        })
      );
    });

    it('fetches user library games successfully', async () => {
      (api.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);

      const { result } = renderHook(
        () => useCarouselGames({ source: 'user-library', limit: 10 }),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getLibrary).toHaveBeenCalled();
      expect(result.current.data?.games).toHaveLength(1);
      expect(result.current.data?.games[0].title).toBe('My Game');
    });

    it('applies sort parameter correctly', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockSharedGamesResponse);

      const { result } = renderHook(
        () => useCarouselGames({ source: 'featured', limit: 10, sort: 'name' }),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(
        expect.objectContaining({
          sortBy: 'title',
          sortDescending: false,
        })
      );
    });

    it('applies rating sort correctly', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockSharedGamesResponse);

      const { result } = renderHook(
        () =>
          useCarouselGames({ source: 'featured', limit: 10, sort: 'rating' }),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(
        expect.objectContaining({
          sortBy: 'averageRating',
          sortDescending: true,
        })
      );
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(
        () => useCarouselGames({ source: 'featured', limit: 10 }, false),
        { wrapper }
      );

      expect(result.current.isFetching).toBe(false);
      expect(api.sharedGames.search).not.toHaveBeenCalled();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch games');
      (api.sharedGames.search as Mock).mockRejectedValue(error);

      const { result } = renderHook(
        () => useCarouselGames({ source: 'featured', limit: 10 }),
        { wrapper }
      );

      // The hook has retry: 3, so we need a longer timeout to wait for all retries
      await waitFor(() => expect(result.current.isError).toBe(true), {
        timeout: 10000,
      });

      expect(result.current.error).toEqual(error);
    });

    it('returns empty games array when no data', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue({
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 10,
        totalPages: 0,
      });

      const { result } = renderHook(
        () => useCarouselGames({ source: 'featured', limit: 10 }),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data?.games).toHaveLength(0);
    });

    it('transforms shared game data to carousel format', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockSharedGamesResponse);

      const { result } = renderHook(
        () => useCarouselGames({ source: 'featured', limit: 10 }),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      const game = result.current.data?.games[0];
      expect(game).toEqual(
        expect.objectContaining({
          id: 'shared-1',
          title: 'Gloomhaven',
          rating: 8.7,
          ratingMax: 10,
        })
      );
      expect(game?.metadata).toBeDefined();
      expect(game?.metadata?.length).toBe(2);
    });

    it('transforms library entry data to carousel format', async () => {
      (api.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);

      const { result } = renderHook(
        () => useCarouselGames({ source: 'user-library', limit: 10 }),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      const game = result.current.data?.games[0];
      expect(game).toEqual(
        expect.objectContaining({
          id: 'game-1',
          title: 'My Game',
          subtitle: 'Publisher A',
        })
      );
    });
  });

  // ==================== useFeaturedGames ====================

  describe('useFeaturedGames', () => {
    it('fetches featured games with correct options', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockSharedGamesResponse);

      const { result } = renderHook(() => useFeaturedGames(8), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(
        expect.objectContaining({
          pageSize: 8,
          sortBy: 'averageRating',
          sortDescending: true,
        })
      );
    });

    it('uses default limit of 10', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockSharedGamesResponse);

      const { result } = renderHook(() => useFeaturedGames(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(
        expect.objectContaining({
          pageSize: 10,
        })
      );
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useFeaturedGames(10, false), {
        wrapper,
      });

      expect(result.current.isFetching).toBe(false);
      expect(api.sharedGames.search).not.toHaveBeenCalled();
    });
  });

  // ==================== useTrendingGames ====================

  describe('useTrendingGames', () => {
    it('fetches trending games with correct options', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockSharedGamesResponse);

      const { result } = renderHook(() => useTrendingGames(8), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(
        expect.objectContaining({
          pageSize: 8,
          sortBy: 'modifiedAt',
          sortDescending: true,
        })
      );
    });

    it('uses default limit of 10', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockSharedGamesResponse);

      const { result } = renderHook(() => useTrendingGames(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(
        expect.objectContaining({
          pageSize: 10,
        })
      );
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useTrendingGames(10, false), {
        wrapper,
      });

      expect(result.current.isFetching).toBe(false);
      expect(api.sharedGames.search).not.toHaveBeenCalled();
    });
  });

  // ==================== useCategoryGames ====================

  describe('useCategoryGames', () => {
    it('fetches category games with correct category', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockSharedGamesResponse);

      const { result } = renderHook(
        () => useCategoryGames('strategy', 8),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(
        expect.objectContaining({
          categoryIds: 'strategy',
          pageSize: 8,
        })
      );
    });

    it('does not fetch when category is empty', () => {
      const { result } = renderHook(() => useCategoryGames('', 10), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.sharedGames.search).not.toHaveBeenCalled();
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(
        () => useCategoryGames('strategy', 10, false),
        { wrapper }
      );

      expect(result.current.isFetching).toBe(false);
      expect(api.sharedGames.search).not.toHaveBeenCalled();
    });
  });

  // ==================== useUserLibraryGames ====================

  describe('useUserLibraryGames', () => {
    it('fetches user library games successfully', async () => {
      (api.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);

      const { result } = renderHook(() => useUserLibraryGames(8), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getLibrary).toHaveBeenCalledWith(
        expect.objectContaining({
          pageSize: 8,
        })
      );
    });

    it('uses default limit of 10', async () => {
      (api.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);

      const { result } = renderHook(() => useUserLibraryGames(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getLibrary).toHaveBeenCalledWith(
        expect.objectContaining({
          pageSize: 10,
        })
      );
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useUserLibraryGames(10, false), {
        wrapper,
      });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getLibrary).not.toHaveBeenCalled();
    });
  });
});
