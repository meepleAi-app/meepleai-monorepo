import { useCallback, useEffect, useState } from 'react';
import { api } from '@/lib/api';
import { AuthUser } from '@/types/auth';

interface GameSummary {
  id: string;
  name: string;
  createdAt: string;
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
      // Note: getSessionStatus returns session info, not user. For user info, use getProfile()
      const session = await api.auth.getSessionStatus();
      if (!session) {
        setAuthUser(null);
        setGames([]);
        return;
      }

      // TODO: Fetch user profile separately if needed (FE-IMP-005)
      // const profile = await api.auth.getProfile();
      // setAuthUser(profile?.user ?? null);
      setAuthUser(null); // Temporary: session status doesn't include user

      const response = await api.games.getAll();
      setGames(response.games.map(g => ({ id: g.id, name: g.title, createdAt: g.createdAt })));
    } catch (err) {
      console.error('Failed to load games', err);
      setError('Unable to load games. Please refresh and try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  // TODO: Re-enable when api.games.create is implemented (FE-IMP-005)
  const createGame = useCallback(
    async (name: string): Promise<GameSummary | null> => {
      setCreating(true);
      setError(null);
      console.error('createGame not yet implemented in modular API');
      setError('Create game functionality not yet available.');
      setCreating(false);
      return null; // Return null instead of throwing
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