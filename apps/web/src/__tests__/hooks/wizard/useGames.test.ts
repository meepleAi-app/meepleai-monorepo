import { renderHook, waitFor, act } from '@testing-library/react';
import { useGames } from '@/hooks/wizard/useGames';
import { api } from '@/lib/api';
import { mockEditor } from '../../fixtures/mockUsers';

jest.mock('@/lib/api', () => ({
  api: {
    games: {
      getAll: jest.fn(),
    },
    post: jest.fn(),
  },
}));

describe('useGames', () => {
  const mockGames = [
    { id: '1', name: 'Gloomhaven', createdAt: '2024-01-01' },
    { id: '2', name: 'Wingspan', createdAt: '2024-01-02' }
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    (api.games.getAll as jest.Mock).mockResolvedValue({
      games: mockGames,
      total: mockGames.length,
      page: 1,
      pageSize: 20,
      totalPages: 1
    });
  });

  describe('Initial Load', () => {
    it('fetches games on mount', async () => {
      const { result } = renderHook(() => useGames(mockEditor));

      expect(result.current.loading).toBe(true);

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.games).toEqual(mockGames);
    });

    it('sets loading to false after fetch completes', async () => {
      const { result } = renderHook(() => useGames(mockEditor));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });
    });

    it('handles empty games list', async () => {
      (api.games.getAll as jest.Mock).mockResolvedValue({
        games: [],
        total: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0
      });

      const { result } = renderHook(() => useGames(mockEditor));

      await waitFor(() => {
        expect(result.current.games).toEqual([]);
      });
    });
  });

  describe('Error Handling', () => {
    it('sets error when fetch fails', async () => {
      (api.games.getAll as jest.Mock).mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => useGames(mockEditor));

      await waitFor(() => {
        expect(result.current.error).toBe('Unable to load games. Please refresh and try again.');
        expect(result.current.loading).toBe(false);
      });
    });

    it('logs error to console when fetch fails', async () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();
      (api.games.getAll as jest.Mock).mockRejectedValue(new Error('Network error'));

      renderHook(() => useGames(mockEditor));

      await waitFor(() => {
        expect(consoleSpy).toHaveBeenCalledWith('Failed to load games', expect.any(Error));
      });

      consoleSpy.mockRestore();
    });
  });

  describe('Game Creation', () => {
    it('creates a new game successfully', async () => {
      const newGame = { id: '3', name: 'Terraforming Mars', createdAt: '2024-01-03' };
      (api.post as jest.Mock).mockResolvedValue({ id: '3', title: 'Terraforming Mars', createdAt: '2024-01-03' });

      const { result } = renderHook(() => useGames(mockEditor));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      let createdGame;
      await act(async () => {
        createdGame = await result.current.createGame('Terraforming Mars');
      });

      expect(createdGame).toEqual(newGame);
      expect(result.current.games).toContainEqual(newGame);
    });

    it('sets creating state during creation', async () => {
      const newGame = { id: '3', name: 'Test', createdAt: '2024-01-03' };
      (api.post as jest.Mock).mockResolvedValue({ id: newGame.id, title: newGame.name, createdAt: newGame.createdAt });

      const { result } = renderHook(() => useGames(mockEditor));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      let createPromise;
      act(() => {
        createPromise = result.current.createGame('Test');
      });

      expect(result.current.creating).toBe(true);

      await act(async () => {
        await createPromise;
      });

      expect(result.current.creating).toBe(false);
    });

    it('adds created game to games list', async () => {
      const newGame = { id: '3', name: 'Azul', createdAt: '2024-01-03' };
      (api.post as jest.Mock).mockResolvedValue({ id: newGame.id, title: newGame.name, createdAt: newGame.createdAt });

      const { result } = renderHook(() => useGames(mockEditor));

      await waitFor(() => {
        expect(result.current.games).toHaveLength(2);
      });

      await act(async () => {
        await result.current.createGame('Azul');
      });

      expect(result.current.games).toHaveLength(3);
      expect(result.current.games[2]).toEqual(newGame);
    });

    it.skip('handles creation error', async () => {
      (api.post as jest.Mock).mockRejectedValue(new Error('Creation failed'));

      const { result } = renderHook(() => useGames(mockEditor));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      await expect(
        act(async () => {
          await result.current.createGame('Test');
        })
      ).rejects.toThrow('Creation failed');

      expect(result.current.error).toBe('Unable to create game. Please try again.');
      expect(result.current.creating).toBe(false);
    });
  });
});