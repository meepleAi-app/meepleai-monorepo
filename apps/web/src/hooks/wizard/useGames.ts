import { useCallback, useEffect, useState } from 'react';
import { api } from '@/lib/api';
import type { AuthUser } from '@/types/auth';

interface GameSummary {
  id: string;
  title: string;
  createdAt: string;
}

/**
 * useGames - Game data fetching and management
 *
 * Issue #1611: Updated to accept user prop from SSR auth
 *
 * Features:
 * - Fetch games list
 * - Create new games
 * - Loading and error states
 * - Automatic refresh on create
 *
 * @param user - Authenticated user from server-side props (optional for backward compat)
 */
export function useGames(user?: AuthUser) {
  const [games, setGames] = useState<GameSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchGames = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await api.games.getAll();
      const fetchedGames = (response?.games ?? []).map(game => ({
        id: game.id,
        title: game.title,
        createdAt: game.createdAt,
      }));
      setGames(fetchedGames);
    } catch (err) {
      console.error('Failed to load games', err);
      setError('Unable to load games. Please refresh and try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  const createGame = useCallback(async (name: string) => {
    setCreating(true);
    setError(null);

    try {
      const createdGame = await api.post<{ id: string; title: string; createdAt: string }>(
        '/api/v1/games',
        { name }
      );
      if (createdGame) {
        const newGame: GameSummary = {
          id: createdGame.id,
          title: createdGame.title,
          createdAt: createdGame.createdAt,
        };
        setGames(prev => [...prev, newGame]);
        return newGame;
      }
    } catch (err) {
      console.error('Failed to create game', err);
      setError('Unable to create game. Please try again.');
      throw err;
    } finally {
      setCreating(false);
    }
  }, []);

  useEffect(() => {
    void fetchGames();
  }, [fetchGames]);

  return {
    games,
    loading,
    creating,
    error,
    createGame,
  };
}
