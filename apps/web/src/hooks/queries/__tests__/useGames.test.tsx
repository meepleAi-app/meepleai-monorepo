/**
 * Tests for useGames hooks
 *
 * Issue #1255: FE-QUALITY — Restore 90% test coverage
 *
 * Tests TanStack Query hooks for games data management
 */

import type { Mock } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useGames, useGame, useGameSessions, useGameDocuments, gamesKeys } from '../useGames';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getAll: vi.fn(),
      getById: vi.fn(),
      getSessions: vi.fn(),
      getDocuments: vi.fn(),
    },
  },
}));

describe('useGames hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  describe('gamesKeys', () => {
    it('generates correct query keys', () => {
      expect(gamesKeys.all).toEqual(['games']);
      expect(gamesKeys.lists()).toEqual(['games', 'list']);
      expect(gamesKeys.list()).toEqual([
        'games',
        'list',
        { filters: undefined, sort: undefined, page: undefined, pageSize: undefined },
      ]);
      expect(gamesKeys.list({ search: 'Catan' })).toEqual([
        'games',
        'list',
        {
          filters: { search: 'Catan' },
          sort: undefined,
          page: undefined,
          pageSize: undefined,
        },
      ]);
      expect(gamesKeys.detail('770e8400-e29b-41d4-a716-000000000001')).toEqual([
        'games',
        'detail',
        '770e8400-e29b-41d4-a716-000000000001',
      ]);
      expect(gamesKeys.sessions('770e8400-e29b-41d4-a716-000000000001')).toEqual([
        'games',
        'sessions',
        '770e8400-e29b-41d4-a716-000000000001',
      ]);
      expect(gamesKeys.documents('770e8400-e29b-41d4-a716-000000000001')).toEqual([
        'games',
        'documents',
        '770e8400-e29b-41d4-a716-000000000001',
      ]);
    });

    it('generates unique keys for different filters', () => {
      const key1 = gamesKeys.list({ search: 'Catan' });
      const key2 = gamesKeys.list({ search: 'Ticket' });
      const key3 = gamesKeys.list({ minPlayers: 2 });

      expect(key1).not.toEqual(key2);
      expect(key1).not.toEqual(key3);
      expect(key2).not.toEqual(key3);
    });

    it('generates unique keys for different pages', () => {
      const key1 = gamesKeys.list(undefined, undefined, 1, 20);
      const key2 = gamesKeys.list(undefined, undefined, 2, 20);

      expect(key1).not.toEqual(key2);
    });
  });

  describe('useGames', () => {
    it('fetches paginated games list', async () => {
      const mockResponse = {
        games: [
          { id: '770e8400-e29b-41d4-a716-000000000001', title: 'Catan', players: '3-4' },
          { id: '770e8400-e29b-41d4-a716-000000000002', title: 'Ticket to Ride', players: '2-5' },
        ],
        total: 2,
        page: 1,
        pageSize: 20,
      };
      (api.games.getAll as Mock).mockResolvedValue(mockResponse);

      const { result } = renderHook(() => useGames(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.games.getAll).toHaveBeenCalledWith(undefined, undefined, 1, 20);
      expect(result.current.data).toEqual(mockResponse);
    });

    it('fetches games with filters', async () => {
      const filters = { search: 'Catan', minPlayers: 2 };
      const mockResponse = {
        games: [{ id: '770e8400-e29b-41d4-a716-000000000001', title: 'Catan', players: '3-4' }],
        total: 1,
        page: 1,
        pageSize: 20,
      };
      (api.games.getAll as Mock).mockResolvedValue(mockResponse);

      const { result } = renderHook(() => useGames(filters), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.games.getAll).toHaveBeenCalledWith(filters, undefined, 1, 20);
      expect(result.current.data).toEqual(mockResponse);
    });

    it('fetches games with sorting', async () => {
      const sort = { field: 'title' as const, direction: 'asc' as const };
      const mockResponse = {
        games: [{ id: '770e8400-e29b-41d4-a716-000000000001', title: 'Catan' }],
        total: 1,
        page: 1,
        pageSize: 20,
      };
      (api.games.getAll as Mock).mockResolvedValue(mockResponse);

      const { result } = renderHook(() => useGames(undefined, sort), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.games.getAll).toHaveBeenCalledWith(undefined, sort, 1, 20);
    });

    it('fetches games with custom pagination', async () => {
      const mockResponse = {
        games: [],
        total: 0,
        page: 3,
        pageSize: 10,
      };
      (api.games.getAll as Mock).mockResolvedValue(mockResponse);

      const { result } = renderHook(() => useGames(undefined, undefined, 3, 10), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.games.getAll).toHaveBeenCalledWith(undefined, undefined, 3, 10);
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch games');
      (api.games.getAll as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useGames(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  describe('useGame', () => {
    it('fetches a single game by ID', async () => {
      const mockGame = {
        id: '770e8400-e29b-41d4-a716-000000000001',
        title: 'Catan',
        players: '3-4',
      };
      (api.games.getById as Mock).mockResolvedValue(mockGame);

      const { result } = renderHook(() => useGame('770e8400-e29b-41d4-a716-000000000001'), {
        wrapper,
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.games.getById).toHaveBeenCalledWith('770e8400-e29b-41d4-a716-000000000001');
      expect(result.current.data).toEqual(mockGame);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useGame('770e8400-e29b-41d4-a716-000000000001', false), {
        wrapper,
      });

      expect(result.current.isFetching).toBe(false);
      expect(api.games.getById).not.toHaveBeenCalled();
    });

    it('throws error when game not found', async () => {
      (api.games.getById as Mock).mockResolvedValue(null);

      const { result } = renderHook(() => useGame('nonexistent'), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(new Error('Game nonexistent not found'));
    });

    it('handles API errors', async () => {
      const error = new Error('Network error');
      (api.games.getById as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useGame('770e8400-e29b-41d4-a716-000000000001'), {
        wrapper,
      });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  describe('useGameSessions', () => {
    // Issue #1951: getSessions is now implemented, mock API properly
    it('fetches game sessions successfully with empty data', async () => {
      const mockSessions: any[] = [];
      (api.games.getSessions as Mock).mockResolvedValue(mockSessions);

      const { result } = renderHook(() => useGameSessions('770e8400-e29b-41d4-a716-000000000001'), {
        wrapper,
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toEqual([]);
      expect(api.games.getSessions).toHaveBeenCalledWith('770e8400-e29b-41d4-a716-000000000001');
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(
        () => useGameSessions('770e8400-e29b-41d4-a716-000000000001', false),
        { wrapper }
      );

      expect(result.current.isFetching).toBe(false);
    });
  });

  describe('useGameDocuments', () => {
    it('fetches documents for a game', async () => {
      const mockDocuments = [
        {
          id: '880e8400-e29b-41d4-a716-000000000001',
          title: 'Rulebook.pdf',
          gameId: '770e8400-e29b-41d4-a716-000000000001',
        },
        {
          id: '880e8400-e29b-41d4-a716-000000000002',
          title: 'Reference.pdf',
          gameId: '770e8400-e29b-41d4-a716-000000000001',
        },
      ];
      (api.games.getDocuments as Mock).mockResolvedValue(mockDocuments);

      const { result } = renderHook(
        () => useGameDocuments('770e8400-e29b-41d4-a716-000000000001'),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.games.getDocuments).toHaveBeenCalledWith('770e8400-e29b-41d4-a716-000000000001');
      expect(result.current.data).toEqual(mockDocuments);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(
        () => useGameDocuments('770e8400-e29b-41d4-a716-000000000001', false),
        { wrapper }
      );

      expect(result.current.isFetching).toBe(false);
      expect(api.games.getDocuments).not.toHaveBeenCalled();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch documents');
      (api.games.getDocuments as Mock).mockRejectedValue(error);

      const { result } = renderHook(
        () => useGameDocuments('770e8400-e29b-41d4-a716-000000000001'),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });

    it('returns empty array for game with no documents', async () => {
      (api.games.getDocuments as Mock).mockResolvedValue([]);

      const { result } = renderHook(
        () => useGameDocuments('770e8400-e29b-41d4-a716-000000000001'),
        { wrapper }
      );

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toEqual([]);
    });
  });
});
