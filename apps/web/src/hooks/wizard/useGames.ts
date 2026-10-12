import { useCallback, useEffect, useState } from 'react';

import { useDebounce } from '@/components/ui/data-display/entity-list-view/hooks/use-debounce';
import { api } from '@/lib/api';
import { logger } from '@/lib/logger';
import type { AuthUser } from '@/types/auth';

interface GameSummary {
  id: string;
  title: string;
  createdAt: string;
}

/** Games per search page. The old list asked for the default (20) and had no search. */
const SEARCH_PAGE_SIZE = 50;

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
 * #4187: the list follows `search` on the server, so a game past the first page can be found.
 * `gamesById` keeps every game seen (and created), so a game chosen under an earlier search
 * can still be named after the search moves on. `loading` covers the first load only.
 *
 * @param user - Authenticated user from server-side props (optional for backward compat)
 * @param search - Text to search the catalog for (default: none)
 */
export function useGames(_user?: AuthUser, search: string = '') {
  const [games, setGames] = useState<GameSummary[]>([]);
  const [gamesById, setGamesById] = useState<Readonly<Record<string, GameSummary>>>({});
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const term = useDebounce(search, 300).trim();

  const remember = useCallback((seen: GameSummary[]) => {
    if (seen.length === 0) return;
    setGamesById(prev => {
      const fresh = seen.filter(g => !prev[g.id]);
      if (fresh.length === 0) return prev;
      const next = { ...prev };
      for (const g of fresh) next[g.id] = g;
      return next;
    });
  }, []);

  const createGame = useCallback(
    async (name: string) => {
      setCreating(true);
      setError(null);

      try {
        const createdGame = await api.games.create(name);

        const newGame: GameSummary = {
          id: createdGame.id,
          title: createdGame.title,
          createdAt: createdGame.createdAt,
        };
        setGames(prev => [...prev, newGame]);
        remember([newGame]);
        return newGame;
      } catch (err) {
        logger.error('Failed to create game', err);
        setError('Unable to create game. Please try again.');
        throw err;
      } finally {
        setCreating(false);
      }
    },
    [remember]
  );

  useEffect(() => {
    let cancelled = false;
    setError(null);

    api.games
      .getAll(term ? { search: term } : undefined, 1, SEARCH_PAGE_SIZE)
      .then(response => {
        if (cancelled) return;
        const fetchedGames = (response?.games ?? []).map(game => ({
          id: game.id,
          title: game.title,
          createdAt: game.createdAt,
        }));
        setGames(fetchedGames);
        remember(fetchedGames);
      })
      .catch(err => {
        if (cancelled) return;
        logger.error('Failed to load games', err);
        setError('Unable to load games. Please refresh and try again.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [term, remember]);

  return {
    games,
    gamesById,
    loading,
    creating,
    error,
    createGame,
  };
}
