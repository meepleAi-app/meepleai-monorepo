/**
 * useGames - TanStack Query hook for games data
 *
 * Issue #1079: FE-IMP-003 — TanStack Query Data Layer
 *
 * Provides automatic caching and pagination for games list.
 */

import { useQuery, UseQueryResult } from '@tanstack/react-query';

import { api, PaginatedGamesResponse, GameFilters } from '@/lib/api';

/**
 * Query key factory for games queries
 */
export const gamesKeys = {
  all: ['games'] as const,
  lists: () => [...gamesKeys.all, 'list'] as const,
  list: (filters?: GameFilters, page?: number, pageSize?: number) =>
    [...gamesKeys.lists(), { filters, page, pageSize }] as const,
  detail: (id: string) => [...gamesKeys.all, 'detail', id] as const,
  sessions: (gameId: string) => [...gamesKeys.all, 'sessions', gameId] as const,
  documents: (gameId: string) => [...gamesKeys.all, 'documents', gameId] as const,
};

/**
 * Hook to fetch a page of the games catalog.
 *
 * Search and pagination run on the server over the whole catalog (#4158). Each
 * filter/page combination has its own cache entry, so a slow response for an older
 * search can never overwrite the newer one.
 *
 * @param filters Optional filters (`search`)
 * @param page Page number (1-indexed)
 * @param pageSize Items per page
 * @returns UseQueryResult with paginated games data
 */
export function useGames(
  filters?: GameFilters,
  page: number = 1,
  pageSize: number = 20,
  // #4187: chi è montato sempre (il pannello chat) la tiene ferma finché non serve.
  enabled: boolean = true
): UseQueryResult<PaginatedGamesResponse, Error> {
  return useQuery({
    queryKey: gamesKeys.list(filters, page, pageSize),
    queryFn: async (): Promise<PaginatedGamesResponse> => {
      return api.games.getAll(filters, page, pageSize);
    },
    enabled,
    // Keep games list fresh for 5 minutes (default staleTime)
    staleTime: 5 * 60 * 1000,
  });
}

/**
 * Hook to fetch single game by ID
 *
 * @param id Game ID
 * @param enabled Whether to run the query (default: true)
 * @returns UseQueryResult with game details
 */
export function useGame(id: string, enabled: boolean = true) {
  return useQuery({
    queryKey: gamesKeys.detail(id),
    queryFn: async () => {
      const game = await api.games.getById(id);
      if (!game) {
        throw new Error(`Game ${id} not found`);
      }
      return game;
    },
    enabled,
    staleTime: 10 * 60 * 1000, // Game details rarely change (10min)
  });
}

/**
 * Hook to fetch game sessions
 *
 * @param gameId Game ID
 * @param enabled Whether to run the query (default: true)
 * @returns UseQueryResult with game sessions
 */
export function useGameSessions(gameId: string, enabled: boolean = true) {
  return useQuery({
    queryKey: gamesKeys.sessions(gameId),
    queryFn: async () => {
      return api.games.getSessions(gameId);
    },
    enabled,
    staleTime: 2 * 60 * 1000, // Sessions change more frequently (2min)
  });
}

/**
 * Hook to fetch game documents (PDFs)
 *
 * @param gameId Game ID
 * @param enabled Whether to run the query (default: true)
 * @returns UseQueryResult with PDF documents
 */
export function useGameDocuments(gameId: string, enabled: boolean = true) {
  return useQuery({
    queryKey: gamesKeys.documents(gameId),
    queryFn: async () => {
      return api.games.getDocuments(gameId);
    },
    enabled,
    staleTime: 5 * 60 * 1000, // Documents don't change often (5min)
  });
}

/**
 * Hook to fetch similar games
 * Issue #3353: Similar Games Discovery with RAG
 *
 * @param gameId Game ID to find similar games for
 * @param options Query options (limit, minSimilarity)
 * @param enabled Whether to run the query (default: true)
 * @returns UseQueryResult with similar games
 */
export function useSimilarGames(
  gameId: string,
  options?: { limit?: number; minSimilarity?: number },
  enabled: boolean = true
) {
  return useQuery({
    queryKey: [...gamesKeys.detail(gameId), 'similar', options] as const,
    queryFn: async () => {
      return api.games.getSimilarGames(gameId, options);
    },
    enabled: enabled && !!gameId,
    staleTime: 10 * 60 * 1000, // Similar games don't change often (10min)
  });
}
