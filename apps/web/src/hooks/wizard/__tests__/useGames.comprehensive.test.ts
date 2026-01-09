/**
 * Comprehensive Tests for useGames wizard hook (Issue #2309)
 *
 * Coverage target: 90%+ (current: 0%)
 * Tests: Game fetching, creation, state management, error handling
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useGames } from '../useGames';
import { api } from '@/lib/api';

// Mock API
vi.mock('@/lib/api');

describe('useGames - Wizard Hook (Issue #2309)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('Initial Load', () => {
    it('should fetch games on mount', async () => {
      const mockGamesResponse = {
        games: [
          { id: 'game-1', title: 'Chess', createdAt: '2024-01-01' },
          { id: 'game-2', title: 'Catan', createdAt: '2024-01-02' },
        ],
        total: 2,
      };

      vi.mocked(api.games.getAll).mockResolvedValueOnce(mockGamesResponse);

      const { result } = renderHook(() => useGames());

      // Should start loading
      expect(result.current.loading).toBe(true);

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.games).toHaveLength(2);
      expect(result.current.games[0].title).toBe('Chess');
      expect(result.current.error).toBeNull();
    });

    it('should handle empty games list', async () => {
      vi.mocked(api.games.getAll).mockResolvedValueOnce({ games: [], total: 0 });

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.games).toEqual([]);
      expect(result.current.error).toBeNull();
    });

    it('should handle null games response', async () => {
      vi.mocked(api.games.getAll).mockResolvedValueOnce({ games: null as any, total: 0 });

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.games).toEqual([]);
    });

    it('should handle fetch error', async () => {
      vi.mocked(api.games.getAll).mockRejectedValueOnce(new Error('Network error'));

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.error).toBe('Unable to load games. Please refresh and try again.');
      expect(result.current.games).toEqual([]);
    });

    it('should set loading state correctly during fetch', async () => {
      let resolveFetch: any;
      vi.mocked(api.games.getAll).mockReturnValueOnce(
        new Promise(resolve => {
          resolveFetch = resolve;
        })
      );

      const { result } = renderHook(() => useGames());

      // Should be loading initially
      expect(result.current.loading).toBe(true);

      // Resolve fetch
      resolveFetch({ games: [], total: 0 });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });
    });
  });

  describe('createGame', () => {
    beforeEach(() => {
      vi.mocked(api.games.getAll).mockResolvedValue({ games: [], total: 0 });
    });

    it('should create game successfully', async () => {
      const mockCreatedGame = {
        id: 'new-game-id',
        title: 'New Game',
        createdAt: '2024-01-03',
      };

      vi.mocked(api.games.create).mockResolvedValueOnce(mockCreatedGame);

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      const createdGame = await result.current.createGame('New Game');

      expect(createdGame).toEqual({
        id: 'new-game-id',
        title: 'New Game',
        createdAt: '2024-01-03',
      });

      expect(api.games.create).toHaveBeenCalledWith('New Game');
    });

    it('should set creating state during game creation', async () => {
      let resolveCreate: any;
      vi.mocked(api.games.create).mockReturnValueOnce(
        new Promise(resolve => {
          resolveCreate = resolve;
        })
      );

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      const createPromise = result.current.createGame('Test Game');

      await waitFor(() => {
        expect(result.current.creating).toBe(true);
      });

      resolveCreate({ id: 'id', title: 'Test Game', createdAt: '2024-01-01' });

      await createPromise;

      await waitFor(() => {
        expect(result.current.creating).toBe(false);
      });
    });

    it('should call create API when creating game', async () => {
      vi.mocked(api.games.create).mockResolvedValueOnce({
        id: 'new-id',
        title: 'New Game',
        createdAt: '2024-01-02',
      });

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      await waitFor(async () => {
        await result.current.createGame('New Game');
      });

      expect(api.games.create).toHaveBeenCalledWith('New Game');
    });

    it('should handle create error', async () => {
      vi.mocked(api.games.create).mockRejectedValueOnce(new Error('Create failed'));

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      await expect(
        waitFor(async () => {
          await result.current.createGame('Failed Game');
        })
      ).rejects.toThrow();

      expect(result.current.error).toBe('Unable to create game. Please try again.');
      expect(result.current.creating).toBe(false);
    });

    it('should not add game to list on create error', async () => {
      vi.mocked(api.games.create).mockRejectedValueOnce(new Error('Error'));

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      const initialLength = result.current.games.length;

      await expect(
        waitFor(async () => {
          await result.current.createGame('Failed');
        })
      ).rejects.toThrow();

      expect(result.current.games).toHaveLength(initialLength);
    });

    it('should reset creating state after error', async () => {
      vi.mocked(api.games.create).mockRejectedValueOnce(new Error('Create error'));

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      await expect(
        waitFor(async () => {
          await result.current.createGame('Fail');
        })
      ).rejects.toThrow();

      expect(result.current.creating).toBe(false);
    });
  });

  describe('Edge Cases', () => {
    it('should handle games with minimal data', async () => {
      const minimalGames = {
        games: [
          {
            id: 'game-1',
            title: 'Game',
            createdAt: '2024-01-01',
            publisher: null,
            yearPublished: null,
          },
        ],
        total: 1,
      };

      vi.mocked(api.games.getAll).mockResolvedValueOnce(minimalGames);

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.games).toHaveLength(1);
      expect(result.current.games[0]).toEqual({
        id: 'game-1',
        title: 'Game',
        createdAt: '2024-01-01',
      });
    });

    it('should handle user parameter (backward compatibility)', async () => {
      vi.mocked(api.games.getAll).mockResolvedValueOnce({ games: [], total: 0 });

      const mockUser = { id: 'user-1', email: 'test@example.com' } as any;

      const { result } = renderHook(() => useGames(mockUser));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.games).toEqual([]);
    });

    it('should not crash with undefined user', async () => {
      vi.mocked(api.games.getAll).mockResolvedValueOnce({ games: [], total: 0 });

      const { result } = renderHook(() => useGames(undefined));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current).toBeDefined();
    });
  });
});
