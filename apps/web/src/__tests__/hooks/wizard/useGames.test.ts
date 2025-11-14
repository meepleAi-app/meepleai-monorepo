import { renderHook, waitFor, act } from '@testing-library/react';
import { useGames } from '@/hooks/wizard/useGames';
import { api } from '@/lib/api';

jest.mock('@/lib/api');

describe('useGames', () => {
  const mockGames = [
    { id: '1', name: 'Gloomhaven', createdAt: '2024-01-01' },
    { id: '2', name: 'Wingspan', createdAt: '2024-01-02' }
  ];

  const mockAuthResponse = {
    user: {
      id: 'user-1',
      email: 'test@example.com',
      displayName: 'Test User',
      role: 'editor'
    },
    expiresAt: '2024-12-31'
  };

  beforeEach(() => {
    jest.clearAllMocks();
    (api.get as jest.Mock).mockImplementation((url) => {
      if (url === '/api/v1/auth/me') {
        return Promise.resolve(mockAuthResponse);
      }
      if (url === '/api/v1/games') {
        return Promise.resolve(mockGames);
      }
      return Promise.resolve(null);
    });
  });

  describe('Initial Load', () => {
    it('fetches games on mount', async () => {
      const { result } = renderHook(() => useGames());

      expect(result.current.loading).toBe(true);

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.games).toEqual(mockGames);
      expect(result.current.authUser).toEqual(mockAuthResponse.user);
    });

    it('sets loading to false after fetch completes', async () => {
      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });
    });

    it('handles unauthenticated user', async () => {
      (api.get as jest.Mock).mockImplementation((url) => {
        if (url === '/api/v1/auth/me') {
          return Promise.resolve(null);
        }
        return Promise.resolve([]);
      });

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.authUser).toBeNull();
        expect(result.current.games).toEqual([]);
      });
    });
  });

  describe('Error Handling', () => {
    it('sets error when fetch fails', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.error).toBe('Unable to load games. Please refresh and try again.');
        expect(result.current.loading).toBe(false);
      });
    });

    it('logs error to console when fetch fails', async () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();
      (api.get as jest.Mock).mockRejectedValue(new Error('Network error'));

      renderHook(() => useGames());

      await waitFor(() => {
        expect(consoleSpy).toHaveBeenCalledWith('Failed to load games', expect.any(Error));
      });

      consoleSpy.mockRestore();
    });
  });

  describe('Create Game', () => {
    it('creates new game successfully', async () => {
      const newGame = { id: '3', name: 'Azul', createdAt: '2024-01-03' };
      (api.post as jest.Mock).mockResolvedValue(newGame);

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      let createdGame;
      await act(async () => {
        createdGame = await result.current.createGame('Azul');
      });

      expect(api.post).toHaveBeenCalledWith('/api/v1/games', { name: 'Azul' });
      expect(createdGame).toEqual(newGame);
      expect(result.current.games).toContainEqual(newGame);
    });

    it('sets creating state during creation', async () => {
      const newGame = { id: '3', name: 'Azul', createdAt: '2024-01-03' };
      let resolveCreate: (value: any) => void;
      const createPromise = new Promise((resolve) => {
        resolveCreate = resolve;
      });
      (api.post as jest.Mock).mockReturnValue(createPromise);

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      act(() => {
        void result.current.createGame('Azul');
      });

      expect(result.current.creating).toBe(true);

      await act(async () => {
        resolveCreate!(newGame);
      });

      await waitFor(() => {
        expect(result.current.creating).toBe(false);
      });
    });

    it('adds created game to games list', async () => {
      const newGame = { id: '3', name: 'Azul', createdAt: '2024-01-03' };
      (api.post as jest.Mock).mockResolvedValue(newGame);

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.games).toHaveLength(2);
      });

      await act(async () => {
        await result.current.createGame('Azul');
      });

      expect(result.current.games).toHaveLength(3);
      expect(result.current.games[2]).toEqual(newGame);
    });

    it('handles creation error', async () => {
      (api.post as jest.Mock).mockRejectedValue(new Error('Creation failed'));

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      await expect(async () => {
        await act(async () => {
          await result.current.createGame('Azul');
        });
      }).rejects.toThrow();

      await waitFor(() => {
        expect(result.current.error).toBe('Unable to create game. Please try again.');
      });
    });

    it('logs error when creation fails', async () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();
      (api.post as jest.Mock).mockRejectedValue(new Error('Creation failed'));

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      try {
        await act(async () => {
          await result.current.createGame('Azul');
        });
      } catch {
        // Expected to throw
      }

      expect(consoleSpy).toHaveBeenCalledWith('Failed to create game', expect.any(Error));
      consoleSpy.mockRestore();
    });
  });

  describe('Refetch', () => {
    it('provides refetch function', async () => {
      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(typeof result.current.refetch).toBe('function');
    });

    it('refetches games when refetch is called', async () => {
      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(api.get).toHaveBeenCalledTimes(2); // Initial: auth + games

      await act(async () => {
        await result.current.refetch();
      });

      expect(api.get).toHaveBeenCalledTimes(4); // Refetch: auth + games again
    });
  });

  describe('Edge Cases', () => {
    it('handles empty games array', async () => {
      (api.get as jest.Mock).mockImplementation((url) => {
        if (url === '/api/v1/auth/me') {
          return Promise.resolve(mockAuthResponse);
        }
        if (url === '/api/v1/games') {
          return Promise.resolve([]);
        }
        return Promise.resolve(null);
      });

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.games).toEqual([]);
        expect(result.current.loading).toBe(false);
      });
    });

    it('handles null games response', async () => {
      (api.get as jest.Mock).mockImplementation((url) => {
        if (url === '/api/v1/auth/me') {
          return Promise.resolve(mockAuthResponse);
        }
        if (url === '/api/v1/games') {
          return Promise.resolve(null);
        }
        return Promise.resolve(null);
      });

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.games).toEqual([]);
      });
    });

    it('handles creation returning null', async () => {
      (api.post as jest.Mock).mockResolvedValue(null);

      const { result } = renderHook(() => useGames());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      const initialCount = result.current.games.length;

      await act(async () => {
        await result.current.createGame('Azul');
      });

      expect(result.current.games).toHaveLength(initialCount); // No change
    });
  });
});