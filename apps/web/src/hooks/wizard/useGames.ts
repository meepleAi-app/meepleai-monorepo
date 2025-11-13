import { useCallback, useEffect, useState } from 'react';
import { api } from '@/lib/api';

interface GameSummary {
  id: string;
  name: string;
  createdAt: string;
}

interface AuthUser {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
}

interface AuthResponse {
  user: AuthUser;
  expiresAt: string;
}

/**
 * useGames - Game data fetching and management
 *
 * Features:
 * - Fetch games list
 * - Create new games
 * - Authentication check
 * - Loading and error states
 * - Automatic refresh on create
 */
export function useGames() {
  const [games, setGames] = useState<GameSummary[]>([]);
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchGames = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const me = await api.get<AuthResponse>('/api/v1/auth/me');
      if (!me) {
        setAuthUser(null);
        setGames([]);
        return;
      }

      setAuthUser(me.user);
      const fetchedGames = (await api.get<GameSummary[]>('/api/v1/games')) ?? [];
      setGames(fetchedGames);
    } catch (err) {
      console.error('Failed to load games', err);
      setError('Unable to load games. Please refresh and try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  const createGame = useCallback(
    async (name: string) => {
      setCreating(true);
      setError(null);

      try {
        const newGame = await api.post<GameSummary>('/api/v1/games', { name });
        if (newGame) {
          setGames((prev) => [...prev, newGame]);
          return newGame;
        }
      } catch (err) {
        console.error('Failed to create game', err);
        setError('Unable to create game. Please try again.');
        throw err;
      } finally {
        setCreating(false);
      }
    },
    []
  );

  useEffect(() => {
    void fetchGames();
  }, [fetchGames]);

  return {
    games,
    authUser,
    loading,
    creating,
    error,
    refetch: fetchGames,
    createGame
  };
}
