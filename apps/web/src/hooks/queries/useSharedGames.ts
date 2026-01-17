/**
 * useSharedGames - TanStack Query hooks for Shared Game Catalog
 *
 * Issue #2518: User Library - Catalog, Library & Agent Configuration UI
 *
 * Provides automatic caching and pagination for shared games catalog.
 */

import { useQuery, UseQueryResult } from '@tanstack/react-query';

import {
  api,
  type PagedSharedGames,
  type SharedGameDetail,
  type GameCategory,
  type GameMechanic,
  type SearchSharedGamesParams,
} from '@/lib/api';

/**
 * Query key factory for shared games queries
 */
export const sharedGamesKeys = {
  all: ['sharedGames'] as const,
  lists: () => [...sharedGamesKeys.all, 'list'] as const,
  list: (params?: SearchSharedGamesParams) => [...sharedGamesKeys.lists(), { params }] as const,
  detail: (id: string) => [...sharedGamesKeys.all, 'detail', id] as const,
  categories: () => [...sharedGamesKeys.all, 'categories'] as const,
  mechanics: () => [...sharedGamesKeys.all, 'mechanics'] as const,
};

/**
 * Hook to search shared games with filters and pagination
 *
 * Features:
 * - Automatic caching per filter/sort/page combination
 * - Pagination support
 * - Advanced filtering (search, categories, mechanics, players, playtime, status)
 *
 * @param params Search and filter parameters
 * @param enabled Whether to run the query (default: true)
 * @returns UseQueryResult with paginated shared games
 */
export function useSharedGames(
  params?: SearchSharedGamesParams,
  enabled: boolean = true
): UseQueryResult<PagedSharedGames, Error> {
  return useQuery({
    queryKey: sharedGamesKeys.list(params),
    queryFn: async (): Promise<PagedSharedGames> => {
      return api.sharedGames.search(params);
    },
    enabled,
    // Shared games catalog changes infrequently (5 minutes)
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Hook to fetch single shared game by ID with full details
 *
 * @param id Game UUID
 * @param enabled Whether to run the query (default: true)
 * @returns UseQueryResult with game details or null
 */
export function useSharedGame(
  id: string,
  enabled: boolean = true
): UseQueryResult<SharedGameDetail | null, Error> {
  return useQuery({
    queryKey: sharedGamesKeys.detail(id),
    queryFn: async () => {
      return api.sharedGames.getById(id);
    },
    enabled: enabled && !!id,
    // Game details rarely change (10 minutes)
    staleTime: 10 * 60 * 1000,
  });
}

/**
 * Hook to fetch all game categories (for filters)
 *
 * @returns UseQueryResult with list of categories
 */
export function useGameCategories(): UseQueryResult<GameCategory[], Error> {
  return useQuery({
    queryKey: sharedGamesKeys.categories(),
    queryFn: async () => {
      return api.sharedGames.getCategories();
    },
    // Categories are static metadata (cache for 1 hour)
    staleTime: 60 * 60 * 1000,
  });
}

/**
 * Hook to fetch all game mechanics (for filters)
 *
 * @returns UseQueryResult with list of mechanics
 */
export function useGameMechanics(): UseQueryResult<GameMechanic[], Error> {
  return useQuery({
    queryKey: sharedGamesKeys.mechanics(),
    queryFn: async () => {
      return api.sharedGames.getMechanics();
    },
    // Mechanics are static metadata (cache for 1 hour)
    staleTime: 60 * 60 * 1000,
  });
}
