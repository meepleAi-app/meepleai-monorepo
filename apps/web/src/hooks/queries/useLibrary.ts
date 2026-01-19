/**
 * useLibrary - TanStack Query hooks for user library data
 *
 * Provides automatic caching and mutations for user game library.
 */

import {
  useQuery,
  useMutation,
  useQueryClient,
  UseQueryResult,
  UseMutationResult,
} from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  PaginatedLibraryResponse,
  UserLibraryStats,
  UserLibraryEntry,
  GameInLibraryStatus,
  LibraryQuotaResponse,
  GetUserLibraryParams,
  AddGameToLibraryRequest,
  UpdateLibraryEntryRequest,
} from '@/lib/api/schemas/library.schemas';

/**
 * Query key factory for library queries
 */
export const libraryKeys = {
  all: ['library'] as const,
  lists: () => [...libraryKeys.all, 'list'] as const,
  list: (params?: GetUserLibraryParams) => [...libraryKeys.lists(), { params }] as const,
  stats: () => [...libraryKeys.all, 'stats'] as const,
  quota: () => [...libraryKeys.all, 'quota'] as const,
  gameStatus: (gameId: string) => [...libraryKeys.all, 'status', gameId] as const,
};

/**
 * Hook to fetch user's game library with pagination and filtering
 *
 * @param params - Optional filtering and pagination parameters
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with paginated library data
 */
export function useLibrary(
  params?: GetUserLibraryParams,
  enabled: boolean = true
): UseQueryResult<PaginatedLibraryResponse, Error> {
  return useQuery({
    queryKey: libraryKeys.list(params),
    queryFn: async (): Promise<PaginatedLibraryResponse> => {
      return api.library.getLibrary(params);
    },
    enabled,
    staleTime: 2 * 60 * 1000, // Library changes frequently (2min)
  });
}

/**
 * Hook to fetch library statistics
 *
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with library stats
 */
export function useLibraryStats(enabled: boolean = true): UseQueryResult<UserLibraryStats, Error> {
  return useQuery({
    queryKey: libraryKeys.stats(),
    queryFn: async (): Promise<UserLibraryStats> => {
      return api.library.getStats();
    },
    enabled,
    staleTime: 2 * 60 * 1000,
  });
}

/**
 * Hook to fetch library quota information (Issue #2445)
 *
 * Returns current usage, tier limits, and remaining slots.
 * Quota changes infrequently so uses longer stale time.
 *
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with library quota data
 */
export function useLibraryQuota(
  enabled: boolean = true
): UseQueryResult<LibraryQuotaResponse, Error> {
  return useQuery({
    queryKey: libraryKeys.quota(),
    queryFn: async (): Promise<LibraryQuotaResponse> => {
      return api.library.getQuota();
    },
    enabled,
    staleTime: 5 * 60 * 1000, // Quota unlikely to change frequently (5min)
  });
}

/**
 * Hook to check if a game is in user's library
 *
 * @param gameId - Game UUID to check
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with game status
 */
export function useGameInLibraryStatus(
  gameId: string,
  enabled: boolean = true
): UseQueryResult<GameInLibraryStatus, Error> {
  return useQuery({
    queryKey: libraryKeys.gameStatus(gameId),
    queryFn: async (): Promise<GameInLibraryStatus> => {
      return api.library.getGameStatus(gameId);
    },
    enabled: enabled && !!gameId,
    staleTime: 30 * 1000, // Status can change frequently (30s)
  });
}

/**
 * Hook to add a game to user's library
 *
 * @returns UseMutationResult for adding game
 */
export function useAddGameToLibrary(): UseMutationResult<
  UserLibraryEntry,
  Error,
  { gameId: string; request?: AddGameToLibraryRequest }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      gameId,
      request,
    }: {
      gameId: string;
      request?: AddGameToLibraryRequest;
    }) => {
      return api.library.addGame(gameId, request);
    },
    onSuccess: (_data, variables) => {
      // Invalidate library list and stats
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      // Invalidate quota (Issue #2445)
      queryClient.invalidateQueries({ queryKey: libraryKeys.quota() });
      // Invalidate game status
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameStatus(variables.gameId) });
    },
  });
}

/**
 * Hook to remove a game from user's library
 *
 * @returns UseMutationResult for removing game
 */
export function useRemoveGameFromLibrary(): UseMutationResult<void, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (gameId: string) => {
      return api.library.removeGame(gameId);
    },
    onSuccess: (_data, gameId) => {
      // Invalidate library list and stats
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      // Invalidate quota (Issue #2445)
      queryClient.invalidateQueries({ queryKey: libraryKeys.quota() });
      // Invalidate game status
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameStatus(gameId) });
    },
  });
}

/**
 * Hook to update a library entry (notes, favorite)
 *
 * @returns UseMutationResult for updating entry
 */
export function useUpdateLibraryEntry(): UseMutationResult<
  UserLibraryEntry,
  Error,
  { gameId: string; request: UpdateLibraryEntryRequest }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      gameId,
      request,
    }: {
      gameId: string;
      request: UpdateLibraryEntryRequest;
    }) => {
      return api.library.updateEntry(gameId, request);
    },
    onSuccess: (_data, variables) => {
      // Invalidate library list and stats
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      // Invalidate game status
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameStatus(variables.gameId) });
    },
  });
}

/**
 * Hook to toggle favorite status of a library entry
 *
 * @returns UseMutationResult for toggling favorite
 */
export function useToggleLibraryFavorite(): UseMutationResult<
  UserLibraryEntry,
  Error,
  { gameId: string; isFavorite: boolean }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ gameId, isFavorite }: { gameId: string; isFavorite: boolean }) => {
      return api.library.updateEntry(gameId, { isFavorite });
    },
    onSuccess: (_data, variables) => {
      // Invalidate library list and stats
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      // Invalidate game status
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameStatus(variables.gameId) });
    },
  });
}

/**
 * Hook to fetch recently added games for dashboard widget (Issue #2612)
 *
 * Returns the most recently added games sorted by addedAt descending.
 * Reuses library query cache for efficiency.
 *
 * @param limit - Number of recent games to fetch (default: 5)
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with paginated library data
 */
export function useRecentlyAddedGames(
  limit: number = 5,
  enabled: boolean = true
): UseQueryResult<PaginatedLibraryResponse, Error> {
  return useLibrary(
    {
      page: 1,
      pageSize: limit,
      sortBy: 'addedAt',
      sortDescending: true,
    },
    enabled
  );
}
