/**
 * Tests for useLibrary hooks
 *
 * Issue #2761: Sprint 1 - Query Hooks Coverage
 *
 * Tests TanStack Query hooks for user library management.
 */

import type { Mock } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import {
  useLibrary,
  useLibraryStats,
  useLibraryQuota,
  useGameInLibraryStatus,
  useAddGameToLibrary,
  useRemoveGameFromLibrary,
  useUpdateLibraryEntry,
  useToggleLibraryFavorite,
  useRecentlyAddedGames,
  useLibraryShareLink,
  useCreateShareLink,
  useUpdateShareLink,
  useRevokeShareLink,
  useSharedLibrary,
  libraryKeys,
} from '../useLibrary';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';
import type {
  PaginatedLibraryResponse,
  UserLibraryStats,
  LibraryQuotaResponse,
  GameInLibraryStatus,
  UserLibraryEntry,
  LibraryShareLink,
  SharedLibrary,
} from '@/lib/api/schemas/library.schemas';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getLibrary: vi.fn(),
      getStats: vi.fn(),
      getQuota: vi.fn(),
      getGameStatus: vi.fn(),
      addGame: vi.fn(),
      removeGame: vi.fn(),
      updateEntry: vi.fn(),
      getShareLink: vi.fn(),
      createShareLink: vi.fn(),
      updateShareLink: vi.fn(),
      revokeShareLink: vi.fn(),
      getSharedLibrary: vi.fn(),
    },
  },
}));

describe('useLibrary hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  // ==================== Query Keys ====================

  describe('libraryKeys', () => {
    it('generates correct base query keys', () => {
      expect(libraryKeys.all).toEqual(['library']);
      expect(libraryKeys.lists()).toEqual(['library', 'list']);
      expect(libraryKeys.stats()).toEqual(['library', 'stats']);
      expect(libraryKeys.quota()).toEqual(['library', 'quota']);
      expect(libraryKeys.shareLink()).toEqual(['library', 'shareLink']);
    });

    it('generates correct list query keys with params', () => {
      expect(libraryKeys.list()).toEqual(['library', 'list', { params: undefined }]);
      expect(libraryKeys.list({ page: 1, pageSize: 20 })).toEqual([
        'library',
        'list',
        { params: { page: 1, pageSize: 20 } },
      ]);
      expect(libraryKeys.list({ sortBy: 'addedAt', sortDescending: true })).toEqual([
        'library',
        'list',
        { params: { sortBy: 'addedAt', sortDescending: true } },
      ]);
    });

    it('generates correct gameStatus query keys', () => {
      const gameId = '770e8400-e29b-41d4-a716-000000000001';
      expect(libraryKeys.gameStatus(gameId)).toEqual(['library', 'status', gameId]);
    });

    it('generates correct sharedLibrary query keys', () => {
      const shareToken = 'abc123token';
      expect(libraryKeys.sharedLibrary(shareToken)).toEqual(['library', 'shared', shareToken]);
    });

    it('generates unique keys for different params', () => {
      const key1 = libraryKeys.list({ page: 1 });
      const key2 = libraryKeys.list({ page: 2 });
      const key3 = libraryKeys.list({ sortBy: 'name' });

      expect(key1).not.toEqual(key2);
      expect(key1).not.toEqual(key3);
      expect(key2).not.toEqual(key3);
    });
  });

  // ==================== useLibrary ====================

  describe('useLibrary', () => {
    const mockLibraryResponse: PaginatedLibraryResponse = {
      items: [
        {
          id: 'lib-1',
          gameId: 'game-1',
          userId: 'user-1',
          game: { id: 'game-1', name: 'Catan', yearPublished: 1995 } as any,
          addedAt: '2024-01-01T00:00:00Z',
          isFavorite: true,
          notes: 'Great game',
          playCount: 5,
        },
        {
          id: 'lib-2',
          gameId: 'game-2',
          userId: 'user-1',
          game: { id: 'game-2', name: 'Ticket to Ride', yearPublished: 2004 } as any,
          addedAt: '2024-01-02T00:00:00Z',
          isFavorite: false,
          playCount: 3,
        },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    };

    it('fetches library items successfully', async () => {
      (api.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);

      const { result } = renderHook(() => useLibrary(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getLibrary).toHaveBeenCalledWith(undefined);
      expect(result.current.data).toEqual(mockLibraryResponse);
      expect(result.current.data?.items).toHaveLength(2);
    });

    it('fetches library with pagination params', async () => {
      (api.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);

      const params = { page: 2, pageSize: 10 };
      const { result } = renderHook(() => useLibrary(params), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getLibrary).toHaveBeenCalledWith(params);
    });

    it('fetches library with sorting params', async () => {
      (api.library.getLibrary as Mock).mockResolvedValue(mockLibraryResponse);

      const params = { sortBy: 'addedAt' as const, sortDescending: true };
      const { result } = renderHook(() => useLibrary(params), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getLibrary).toHaveBeenCalledWith(params);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useLibrary(undefined, false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getLibrary).not.toHaveBeenCalled();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch library');
      (api.library.getLibrary as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useLibrary(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });

    it('returns empty list when library is empty', async () => {
      const emptyResponse: PaginatedLibraryResponse = {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
      };
      (api.library.getLibrary as Mock).mockResolvedValue(emptyResponse);

      const { result } = renderHook(() => useLibrary(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data?.items).toHaveLength(0);
      expect(result.current.data?.totalCount).toBe(0);
    });
  });

  // ==================== useLibraryStats ====================

  describe('useLibraryStats', () => {
    const mockStats: UserLibraryStats = {
      totalGames: 50,
      favorites: 10,
      recentlyAdded: 5,
      totalPlayCount: 100,
    };

    it('fetches library stats successfully', async () => {
      (api.library.getStats as Mock).mockResolvedValue(mockStats);

      const { result } = renderHook(() => useLibraryStats(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getStats).toHaveBeenCalled();
      expect(result.current.data).toEqual(mockStats);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useLibraryStats(false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getStats).not.toHaveBeenCalled();
    });

    it('handles stats fetch errors', async () => {
      const error = new Error('Failed to fetch stats');
      (api.library.getStats as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useLibraryStats(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useLibraryQuota ====================

  describe('useLibraryQuota', () => {
    const mockQuota: LibraryQuotaResponse = {
      currentCount: 50,
      maxCount: 100,
      tier: 'premium',
      remainingSlots: 50,
      percentUsed: 50,
    };

    it('fetches library quota successfully', async () => {
      (api.library.getQuota as Mock).mockResolvedValue(mockQuota);

      const { result } = renderHook(() => useLibraryQuota(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getQuota).toHaveBeenCalled();
      expect(result.current.data).toEqual(mockQuota);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useLibraryQuota(false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getQuota).not.toHaveBeenCalled();
    });

    it('handles quota fetch errors', async () => {
      const error = new Error('Failed to fetch quota');
      (api.library.getQuota as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useLibraryQuota(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useGameInLibraryStatus ====================

  describe('useGameInLibraryStatus', () => {
    const gameId = '770e8400-e29b-41d4-a716-000000000001';
    const mockStatus: GameInLibraryStatus = {
      inLibrary: true,
      libraryEntryId: 'lib-entry-1',
      isFavorite: true,
      addedAt: '2024-01-01T00:00:00Z',
    };

    it('fetches game status successfully', async () => {
      (api.library.getGameStatus as Mock).mockResolvedValue(mockStatus);

      const { result } = renderHook(() => useGameInLibraryStatus(gameId), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getGameStatus).toHaveBeenCalledWith(gameId);
      expect(result.current.data).toEqual(mockStatus);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useGameInLibraryStatus(gameId, false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getGameStatus).not.toHaveBeenCalled();
    });

    it('does not fetch when gameId is empty', () => {
      const { result } = renderHook(() => useGameInLibraryStatus(''), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getGameStatus).not.toHaveBeenCalled();
    });

    it('handles status fetch errors', async () => {
      const error = new Error('Game not found');
      (api.library.getGameStatus as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useGameInLibraryStatus(gameId), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useAddGameToLibrary ====================

  describe('useAddGameToLibrary', () => {
    const gameId = '770e8400-e29b-41d4-a716-000000000001';
    const mockEntry: UserLibraryEntry = {
      id: 'lib-entry-new',
      gameId,
      userId: 'user-1',
      game: { id: gameId, name: 'New Game' } as any,
      addedAt: new Date().toISOString(),
      isFavorite: false,
      playCount: 0,
    };

    it('adds game to library successfully', async () => {
      (api.library.addGame as Mock).mockResolvedValue(mockEntry);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useAddGameToLibrary(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync({ gameId });
      });

      expect(api.library.addGame).toHaveBeenCalledWith(gameId, undefined);

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data).toEqual(mockEntry);

      // Verify cache invalidation
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.lists() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.stats() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.quota() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.gameStatus(gameId) });
    });

    it('adds game with request options', async () => {
      (api.library.addGame as Mock).mockResolvedValue(mockEntry);

      const { result } = renderHook(() => useAddGameToLibrary(), { wrapper });

      const request = { notes: 'My notes', isFavorite: true };
      await act(async () => {
        await result.current.mutateAsync({ gameId, request });
      });

      expect(api.library.addGame).toHaveBeenCalledWith(gameId, request);
    });

    it('handles add errors', async () => {
      const error = new Error('Failed to add game');
      (api.library.addGame as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useAddGameToLibrary(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync({ gameId });
        } catch (e) {
          expect(e).toEqual(error);
        }
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useRemoveGameFromLibrary ====================

  describe('useRemoveGameFromLibrary', () => {
    const gameId = '770e8400-e29b-41d4-a716-000000000001';

    it('removes game from library successfully', async () => {
      (api.library.removeGame as Mock).mockResolvedValue(undefined);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useRemoveGameFromLibrary(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync(gameId);
      });

      expect(api.library.removeGame).toHaveBeenCalledWith(gameId);

      // Verify cache invalidation
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.lists() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.stats() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.quota() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.gameStatus(gameId) });
    });

    it('handles remove errors', async () => {
      const error = new Error('Failed to remove game');
      (api.library.removeGame as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useRemoveGameFromLibrary(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync(gameId);
        } catch (e) {
          expect(e).toEqual(error);
        }
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useUpdateLibraryEntry ====================

  describe('useUpdateLibraryEntry', () => {
    const gameId = '770e8400-e29b-41d4-a716-000000000001';
    const mockUpdatedEntry: UserLibraryEntry = {
      id: 'lib-entry-1',
      gameId,
      userId: 'user-1',
      game: { id: gameId, name: 'Catan' } as any,
      addedAt: '2024-01-01T00:00:00Z',
      isFavorite: true,
      notes: 'Updated notes',
      playCount: 10,
    };

    it('updates library entry successfully', async () => {
      (api.library.updateEntry as Mock).mockResolvedValue(mockUpdatedEntry);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useUpdateLibraryEntry(), { wrapper });

      const request = { notes: 'Updated notes', playCount: 10 };
      await act(async () => {
        await result.current.mutateAsync({ gameId, request });
      });

      expect(api.library.updateEntry).toHaveBeenCalledWith(gameId, request);

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data).toEqual(mockUpdatedEntry);

      // Verify cache invalidation
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.lists() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.stats() });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.gameStatus(gameId) });
    });

    it('handles update errors', async () => {
      const error = new Error('Failed to update entry');
      (api.library.updateEntry as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useUpdateLibraryEntry(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync({ gameId, request: { notes: 'test' } });
        } catch (e) {
          expect(e).toEqual(error);
        }
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useToggleLibraryFavorite ====================

  describe('useToggleLibraryFavorite', () => {
    const gameId = '770e8400-e29b-41d4-a716-000000000001';
    const mockEntry: UserLibraryEntry = {
      id: 'lib-entry-1',
      gameId,
      userId: 'user-1',
      game: { id: gameId, name: 'Catan' } as any,
      addedAt: '2024-01-01T00:00:00Z',
      isFavorite: true,
      playCount: 5,
    };

    it('toggles favorite on successfully', async () => {
      (api.library.updateEntry as Mock).mockResolvedValue(mockEntry);

      const { result } = renderHook(() => useToggleLibraryFavorite(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync({ gameId, isFavorite: true });
      });

      expect(api.library.updateEntry).toHaveBeenCalledWith(gameId, { isFavorite: true });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data?.isFavorite).toBe(true);
    });

    it('toggles favorite off successfully', async () => {
      const unfavoritedEntry = { ...mockEntry, isFavorite: false };
      (api.library.updateEntry as Mock).mockResolvedValue(unfavoritedEntry);

      const { result } = renderHook(() => useToggleLibraryFavorite(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync({ gameId, isFavorite: false });
      });

      expect(api.library.updateEntry).toHaveBeenCalledWith(gameId, { isFavorite: false });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data?.isFavorite).toBe(false);
    });
  });

  // ==================== useRecentlyAddedGames ====================

  describe('useRecentlyAddedGames', () => {
    const mockRecentGames: PaginatedLibraryResponse = {
      items: [
        {
          id: 'lib-1',
          gameId: 'game-1',
          userId: 'user-1',
          game: { id: 'game-1', name: 'Recent Game 1' } as any,
          addedAt: '2024-01-03T00:00:00Z',
          isFavorite: false,
          playCount: 0,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 5,
      totalPages: 1,
    };

    it('fetches recently added games with default limit', async () => {
      (api.library.getLibrary as Mock).mockResolvedValue(mockRecentGames);

      const { result } = renderHook(() => useRecentlyAddedGames(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getLibrary).toHaveBeenCalledWith({
        page: 1,
        pageSize: 5,
        sortBy: 'addedAt',
        sortDescending: true,
      });
    });

    it('fetches recently added games with custom limit', async () => {
      (api.library.getLibrary as Mock).mockResolvedValue(mockRecentGames);

      const { result } = renderHook(() => useRecentlyAddedGames(10), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getLibrary).toHaveBeenCalledWith({
        page: 1,
        pageSize: 10,
        sortBy: 'addedAt',
        sortDescending: true,
      });
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useRecentlyAddedGames(5, false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getLibrary).not.toHaveBeenCalled();
    });
  });

  // ==================== Share Link Hooks ====================

  describe('useLibraryShareLink', () => {
    const mockShareLink: LibraryShareLink = {
      token: 'share-token-123',
      userId: 'user-1',
      privacyLevel: 'public',
      includeNotes: false,
      expiresAt: '2025-01-01T00:00:00Z',
      createdAt: '2024-01-01T00:00:00Z',
      viewCount: 10,
    };

    it('fetches share link successfully', async () => {
      (api.library.getShareLink as Mock).mockResolvedValue(mockShareLink);

      const { result } = renderHook(() => useLibraryShareLink(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getShareLink).toHaveBeenCalled();
      expect(result.current.data).toEqual(mockShareLink);
    });

    it('returns null when no share link exists', async () => {
      (api.library.getShareLink as Mock).mockResolvedValue(null);

      const { result } = renderHook(() => useLibraryShareLink(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toBeNull();
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useLibraryShareLink(false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getShareLink).not.toHaveBeenCalled();
    });
  });

  describe('useCreateShareLink', () => {
    const mockShareLink: LibraryShareLink = {
      token: 'new-share-token',
      userId: 'user-1',
      privacyLevel: 'friends',
      includeNotes: true,
      createdAt: new Date().toISOString(),
      viewCount: 0,
    };

    it('creates share link successfully', async () => {
      (api.library.createShareLink as Mock).mockResolvedValue(mockShareLink);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useCreateShareLink(), { wrapper });

      const request = { privacyLevel: 'friends' as const, includeNotes: true };
      await act(async () => {
        await result.current.mutateAsync(request);
      });

      expect(api.library.createShareLink).toHaveBeenCalledWith(request);

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data).toEqual(mockShareLink);
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.shareLink() });
    });
  });

  describe('useUpdateShareLink', () => {
    const mockUpdatedLink: LibraryShareLink = {
      token: 'share-token-123',
      userId: 'user-1',
      privacyLevel: 'public',
      includeNotes: true,
      createdAt: '2024-01-01T00:00:00Z',
      viewCount: 10,
    };

    it('updates share link successfully', async () => {
      (api.library.updateShareLink as Mock).mockResolvedValue(mockUpdatedLink);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useUpdateShareLink(), { wrapper });

      const shareToken = 'share-token-123';
      const request = { includeNotes: true };
      await act(async () => {
        await result.current.mutateAsync({ shareToken, request });
      });

      expect(api.library.updateShareLink).toHaveBeenCalledWith(shareToken, request);

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data).toEqual(mockUpdatedLink);
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.shareLink() });
    });
  });

  describe('useRevokeShareLink', () => {
    it('revokes share link successfully', async () => {
      (api.library.revokeShareLink as Mock).mockResolvedValue(undefined);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useRevokeShareLink(), { wrapper });

      const shareToken = 'share-token-123';
      await act(async () => {
        await result.current.mutateAsync(shareToken);
      });

      expect(api.library.revokeShareLink).toHaveBeenCalledWith(shareToken);
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: libraryKeys.shareLink() });
    });
  });

  describe('useSharedLibrary', () => {
    const shareToken = 'valid-share-token';
    const mockSharedLibrary: SharedLibrary = {
      ownerName: 'John Doe',
      privacyLevel: 'public',
      games: [
        {
          id: 'lib-1',
          gameId: 'game-1',
          userId: 'user-1',
          game: { id: 'game-1', name: 'Shared Game' } as any,
          addedAt: '2024-01-01T00:00:00Z',
          isFavorite: true,
          playCount: 5,
        },
      ],
      totalGames: 1,
      sharedAt: '2024-01-01T00:00:00Z',
    };

    it('fetches shared library successfully', async () => {
      (api.library.getSharedLibrary as Mock).mockResolvedValue(mockSharedLibrary);

      const { result } = renderHook(() => useSharedLibrary(shareToken), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.library.getSharedLibrary).toHaveBeenCalledWith(shareToken);
      expect(result.current.data).toEqual(mockSharedLibrary);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useSharedLibrary(shareToken, false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getSharedLibrary).not.toHaveBeenCalled();
    });

    it('does not fetch when shareToken is empty', () => {
      const { result } = renderHook(() => useSharedLibrary(''), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.library.getSharedLibrary).not.toHaveBeenCalled();
    });

    it('returns null for invalid/expired token', async () => {
      (api.library.getSharedLibrary as Mock).mockResolvedValue(null);

      const { result } = renderHook(() => useSharedLibrary('invalid-token'), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toBeNull();
    });

    it('handles fetch errors (does not retry on 404)', async () => {
      const error = new Error('Not found');
      (api.library.getSharedLibrary as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useSharedLibrary(shareToken), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });
});
