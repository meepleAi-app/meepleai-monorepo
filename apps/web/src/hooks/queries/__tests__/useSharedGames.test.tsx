/**
 * Tests for useSharedGames hooks
 *
 * Issue #2761: Sprint 1 - Query Hooks Coverage
 *
 * Tests TanStack Query hooks for shared games catalog.
 */

import type { Mock } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import {
  useSharedGames,
  useSharedGame,
  useGameCategories,
  useGameMechanics,
  sharedGamesKeys,
} from '../useSharedGames';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      search: vi.fn(),
      getById: vi.fn(),
      getCategories: vi.fn(),
      getMechanics: vi.fn(),
    },
  },
}));

describe('useSharedGames hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  // ==================== Query Keys ====================

  describe('sharedGamesKeys', () => {
    it('generates correct base query keys', () => {
      expect(sharedGamesKeys.all).toEqual(['sharedGames']);
      expect(sharedGamesKeys.lists()).toEqual(['sharedGames', 'list']);
      expect(sharedGamesKeys.categories()).toEqual(['sharedGames', 'categories']);
      expect(sharedGamesKeys.mechanics()).toEqual(['sharedGames', 'mechanics']);
    });

    it('generates correct list query keys with params', () => {
      expect(sharedGamesKeys.list()).toEqual(['sharedGames', 'list', { params: undefined }]);
      expect(sharedGamesKeys.list({ search: 'Catan' })).toEqual([
        'sharedGames',
        'list',
        { params: { search: 'Catan' } },
      ]);
      expect(sharedGamesKeys.list({ page: 2, pageSize: 10 })).toEqual([
        'sharedGames',
        'list',
        { params: { page: 2, pageSize: 10 } },
      ]);
    });

    it('generates correct detail query keys', () => {
      const gameId = '770e8400-e29b-41d4-a716-000000000001';
      expect(sharedGamesKeys.detail(gameId)).toEqual(['sharedGames', 'detail', gameId]);
    });

    it('generates unique keys for different params', () => {
      const key1 = sharedGamesKeys.list({ search: 'Catan' });
      const key2 = sharedGamesKeys.list({ search: 'Ticket' });
      const key3 = sharedGamesKeys.list({ categories: ['strategy'] });

      expect(key1).not.toEqual(key2);
      expect(key1).not.toEqual(key3);
    });
  });

  // ==================== useSharedGames ====================

  describe('useSharedGames', () => {
    const mockGamesResponse = {
      items: [
        {
          id: 'shared-1',
          name: 'Catan',
          yearPublished: 1995,
          minPlayers: 3,
          maxPlayers: 4,
          playingTime: 90,
          complexity: 2.32,
          rating: 7.2,
          categories: ['strategy'],
          mechanics: ['trading', 'dice-rolling'],
        },
        {
          id: 'shared-2',
          name: 'Ticket to Ride',
          yearPublished: 2004,
          minPlayers: 2,
          maxPlayers: 5,
          playingTime: 60,
          complexity: 1.85,
          rating: 7.5,
          categories: ['family'],
          mechanics: ['set-collection'],
        },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    };

    it('fetches shared games successfully', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockGamesResponse);

      const { result } = renderHook(() => useSharedGames(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(undefined);
      expect(result.current.data).toEqual(mockGamesResponse);
      expect(result.current.data?.items).toHaveLength(2);
    });

    it('fetches shared games with search param', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockGamesResponse);

      const params = { search: 'Catan' };
      const { result } = renderHook(() => useSharedGames(params), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(params);
    });

    it('fetches shared games with pagination', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockGamesResponse);

      const params = { page: 2, pageSize: 10 };
      const { result } = renderHook(() => useSharedGames(params), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(params);
    });

    it('fetches shared games with category filter', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockGamesResponse);

      const params = { categories: ['strategy', 'economic'] };
      const { result } = renderHook(() => useSharedGames(params), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(params);
    });

    it('fetches shared games with mechanics filter', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockGamesResponse);

      const params = { mechanics: ['worker-placement', 'deck-building'] };
      const { result } = renderHook(() => useSharedGames(params), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(params);
    });

    it('fetches shared games with player count filter', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockGamesResponse);

      const params = { minPlayers: 2, maxPlayers: 4 };
      const { result } = renderHook(() => useSharedGames(params), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(params);
    });

    it('fetches shared games with complexity filter', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockGamesResponse);

      const params = { minComplexity: 2.0, maxComplexity: 3.5 };
      const { result } = renderHook(() => useSharedGames(params), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(params);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useSharedGames(undefined, false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.sharedGames.search).not.toHaveBeenCalled();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch shared games');
      (api.sharedGames.search as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useSharedGames(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });

    it('returns empty list when no games found', async () => {
      const emptyResponse = {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
      };
      (api.sharedGames.search as Mock).mockResolvedValue(emptyResponse);

      const { result } = renderHook(() => useSharedGames({ search: 'nonexistent' }), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data?.items).toHaveLength(0);
      expect(result.current.data?.totalCount).toBe(0);
    });

    it('supports combined filters', async () => {
      (api.sharedGames.search as Mock).mockResolvedValue(mockGamesResponse);

      const params = {
        search: 'strategy',
        categories: ['strategy'],
        mechanics: ['worker-placement'],
        minPlayers: 2,
        maxPlayers: 4,
        minComplexity: 2.0,
        maxComplexity: 4.0,
        page: 1,
        pageSize: 20,
      };
      const { result } = renderHook(() => useSharedGames(params), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.search).toHaveBeenCalledWith(params);
    });
  });

  // ==================== useSharedGame ====================

  describe('useSharedGame', () => {
    const gameId = '770e8400-e29b-41d4-a716-000000000001';
    const mockGameDetail = {
      id: gameId,
      name: 'Catan',
      yearPublished: 1995,
      description: 'Collect and trade resources to build settlements.',
      minPlayers: 3,
      maxPlayers: 4,
      playingTime: 90,
      minPlayTime: 60,
      maxPlayTime: 120,
      complexity: 2.32,
      rating: 7.2,
      numRatings: 125000,
      thumbnailUrl: 'https://example.com/catan-thumb.jpg',
      imageUrl: 'https://example.com/catan.jpg',
      categories: ['strategy', 'economic'],
      mechanics: ['trading', 'dice-rolling', 'modular-board'],
      designers: ['Klaus Teuber'],
      publishers: ['Kosmos'],
    };

    it('fetches game detail successfully', async () => {
      (api.sharedGames.getById as Mock).mockResolvedValue(mockGameDetail);

      const { result } = renderHook(() => useSharedGame(gameId), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.getById).toHaveBeenCalledWith(gameId);
      expect(result.current.data).toEqual(mockGameDetail);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useSharedGame(gameId, false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.sharedGames.getById).not.toHaveBeenCalled();
    });

    it('does not fetch when gameId is empty', () => {
      const { result } = renderHook(() => useSharedGame(''), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.sharedGames.getById).not.toHaveBeenCalled();
    });

    it('handles game not found', async () => {
      (api.sharedGames.getById as Mock).mockResolvedValue(null);

      const { result } = renderHook(() => useSharedGame('nonexistent-id'), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toBeNull();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch game');
      (api.sharedGames.getById as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useSharedGame(gameId), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useGameCategories ====================

  describe('useGameCategories', () => {
    const mockCategories = [
      { id: 'strategy', name: 'Strategy', count: 150 },
      { id: 'family', name: 'Family', count: 120 },
      { id: 'party', name: 'Party Games', count: 80 },
      { id: 'cooperative', name: 'Cooperative', count: 70 },
    ];

    it('fetches categories successfully', async () => {
      (api.sharedGames.getCategories as Mock).mockResolvedValue(mockCategories);

      const { result } = renderHook(() => useGameCategories(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.getCategories).toHaveBeenCalled();
      expect(result.current.data).toEqual(mockCategories);
      expect(result.current.data).toHaveLength(4);
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch categories');
      (api.sharedGames.getCategories as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useGameCategories(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });

    it('returns empty array when no categories', async () => {
      (api.sharedGames.getCategories as Mock).mockResolvedValue([]);

      const { result } = renderHook(() => useGameCategories(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toEqual([]);
    });
  });

  // ==================== useGameMechanics ====================

  describe('useGameMechanics', () => {
    const mockMechanics = [
      { id: 'worker-placement', name: 'Worker Placement', count: 85 },
      { id: 'deck-building', name: 'Deck Building', count: 65 },
      { id: 'area-control', name: 'Area Control', count: 90 },
      { id: 'dice-rolling', name: 'Dice Rolling', count: 120 },
      { id: 'hand-management', name: 'Hand Management', count: 150 },
    ];

    it('fetches mechanics successfully', async () => {
      (api.sharedGames.getMechanics as Mock).mockResolvedValue(mockMechanics);

      const { result } = renderHook(() => useGameMechanics(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.sharedGames.getMechanics).toHaveBeenCalled();
      expect(result.current.data).toEqual(mockMechanics);
      expect(result.current.data).toHaveLength(5);
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch mechanics');
      (api.sharedGames.getMechanics as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useGameMechanics(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });

    it('returns empty array when no mechanics', async () => {
      (api.sharedGames.getMechanics as Mock).mockResolvedValue([]);

      const { result } = renderHook(() => useGameMechanics(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toEqual([]);
    });
  });
});
