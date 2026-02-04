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
  UpdateGameStateRequest,
  LibraryShareLink,
  CreateLibraryShareLinkRequest,
  UpdateLibraryShareLinkRequest,
  SharedLibrary,
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
  // Game detail key (Issue #3513)
  gameDetail: (gameId: string) => [...libraryKeys.all, 'detail', gameId] as const,
  // Share link keys (Issue #2614)
  shareLink: () => [...libraryKeys.all, 'shareLink'] as const,
  sharedLibrary: (shareToken: string) => [...libraryKeys.all, 'shared', shareToken] as const,
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
 * Hook to add a game to user's library with optimistic update
 *
 * Issue #2859: Optimistic updates for quick actions
 * - Immediately updates game status to 'InLibrary' in cache
 * - Optimistically updates quota (currentCount + 1, remainingSlots - 1)
 * - Rolls back on error
 * - Refetches on settlement for consistency
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
    onMutate: async ({ gameId }) => {
      // Cancel any outgoing refetches to avoid overwriting optimistic update
      await queryClient.cancelQueries({ queryKey: libraryKeys.gameStatus(gameId) });
      await queryClient.cancelQueries({ queryKey: libraryKeys.quota() });

      // Snapshot previous values for rollback
      const previousGameStatus = queryClient.getQueryData<GameInLibraryStatus>(
        libraryKeys.gameStatus(gameId)
      );
      const previousQuota = queryClient.getQueryData<LibraryQuotaResponse>(libraryKeys.quota());

      // Optimistically update game status to 'InLibrary'
      queryClient.setQueryData<GameInLibraryStatus>(
        libraryKeys.gameStatus(gameId),
        (old) => (old ? { ...old, inLibrary: true } : { inLibrary: true, isFavorite: false })
      );

      // Optimistically update quota
      if (previousQuota) {
        queryClient.setQueryData<LibraryQuotaResponse>(libraryKeys.quota(), (old) => {
          if (!old) return old;
          const newCount = old.currentCount + 1;
          const newRemaining = Math.max(0, old.remainingSlots - 1);
          return {
            ...old,
            currentCount: newCount,
            remainingSlots: newRemaining,
            percentageUsed: (newCount / old.maxAllowed) * 100,
          };
        });
      }

      return { previousGameStatus, previousQuota };
    },
    onError: (_err, { gameId }, context) => {
      // Rollback to previous data on error
      if (context?.previousGameStatus) {
        queryClient.setQueryData(libraryKeys.gameStatus(gameId), context.previousGameStatus);
      }
      if (context?.previousQuota) {
        queryClient.setQueryData(libraryKeys.quota(), context.previousQuota);
      }
    },
    onSettled: (_data, _err, { gameId }) => {
      // Refetch to ensure cache consistency with server
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.quota() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameStatus(gameId) });
    },
  });
}

/**
 * Hook to remove a game from user's library with optimistic update
 *
 * Issue #2859: Optimistic updates for quick actions
 * - Immediately updates game status to not in library
 * - Optimistically updates quota (currentCount - 1, remainingSlots + 1)
 * - Removes game from library list cache
 * - Rolls back on error
 * - Refetches on settlement for consistency
 *
 * @returns UseMutationResult for removing game
 */
export function useRemoveGameFromLibrary(): UseMutationResult<void, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (gameId: string) => {
      return api.library.removeGame(gameId);
    },
    onMutate: async (gameId) => {
      // Cancel any outgoing refetches to avoid overwriting optimistic update
      await queryClient.cancelQueries({ queryKey: libraryKeys.lists() });
      await queryClient.cancelQueries({ queryKey: libraryKeys.gameStatus(gameId) });
      await queryClient.cancelQueries({ queryKey: libraryKeys.quota() });

      // Snapshot previous values for rollback
      const previousLibrary = queryClient.getQueriesData<PaginatedLibraryResponse>({
        queryKey: libraryKeys.lists(),
      });
      const previousGameStatus = queryClient.getQueryData<GameInLibraryStatus>(
        libraryKeys.gameStatus(gameId)
      );
      const previousQuota = queryClient.getQueryData<LibraryQuotaResponse>(libraryKeys.quota());
      const previousStats = queryClient.getQueryData<UserLibraryStats>(libraryKeys.stats());

      // Optimistically remove game from library lists
      queryClient.setQueriesData<PaginatedLibraryResponse>(
        { queryKey: libraryKeys.lists() },
        (old) => {
          if (!old) return old;
          return {
            ...old,
            items: old.items.filter((entry) => entry.gameId !== gameId),
            totalCount: Math.max(0, old.totalCount - 1),
          };
        }
      );

      // Optimistically update game status to not in library
      queryClient.setQueryData<GameInLibraryStatus>(
        libraryKeys.gameStatus(gameId),
        (old) => (old ? { ...old, inLibrary: false, isFavorite: false } : { inLibrary: false, isFavorite: false })
      );

      // Optimistically update quota
      if (previousQuota) {
        queryClient.setQueryData<LibraryQuotaResponse>(libraryKeys.quota(), (old) => {
          if (!old) return old;
          const newCount = Math.max(0, old.currentCount - 1);
          const newRemaining = old.remainingSlots + 1;
          return {
            ...old,
            currentCount: newCount,
            remainingSlots: Math.min(newRemaining, old.maxAllowed),
            percentageUsed: (newCount / old.maxAllowed) * 100,
          };
        });
      }

      // Optimistically update stats
      if (previousStats) {
        queryClient.setQueryData<UserLibraryStats>(libraryKeys.stats(), (old) => {
          if (!old) return old;
          return {
            ...old,
            totalGames: Math.max(0, old.totalGames - 1),
          };
        });
      }

      return { previousLibrary, previousGameStatus, previousQuota, previousStats };
    },
    onError: (_err, gameId, context) => {
      // Rollback to previous data on error
      if (context?.previousLibrary) {
        context.previousLibrary.forEach(([queryKey, data]) => {
          queryClient.setQueryData(queryKey, data);
        });
      }
      if (context?.previousGameStatus) {
        queryClient.setQueryData(libraryKeys.gameStatus(gameId), context.previousGameStatus);
      }
      if (context?.previousQuota) {
        queryClient.setQueryData(libraryKeys.quota(), context.previousQuota);
      }
      if (context?.previousStats) {
        queryClient.setQueryData(libraryKeys.stats(), context.previousStats);
      }
    },
    onSettled: (_data, _err, gameId) => {
      // Refetch to ensure cache consistency with server
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.quota() });
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
 * Hook to toggle favorite status of a library entry with optimistic update
 *
 * Issue #2859: Optimistic updates for quick actions
 * - Immediately updates isFavorite flag in cache
 * - Rolls back on error
 * - Refetches on settlement for consistency
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
    onMutate: async ({ gameId, isFavorite }) => {
      // Cancel any outgoing refetches to avoid overwriting optimistic update
      await queryClient.cancelQueries({ queryKey: libraryKeys.lists() });
      await queryClient.cancelQueries({ queryKey: libraryKeys.stats() });

      // Snapshot previous values for rollback
      const previousLibrary = queryClient.getQueriesData<PaginatedLibraryResponse>({
        queryKey: libraryKeys.lists(),
      });
      const previousStats = queryClient.getQueryData<UserLibraryStats>(libraryKeys.stats());
      const previousGameStatus = queryClient.getQueryData<GameInLibraryStatus>(
        libraryKeys.gameStatus(gameId)
      );

      // Optimistically update library lists
      queryClient.setQueriesData<PaginatedLibraryResponse>(
        { queryKey: libraryKeys.lists() },
        (old) => {
          if (!old) return old;
          return {
            ...old,
            items: old.items.map((entry) =>
              entry.gameId === gameId ? { ...entry, isFavorite } : entry
            ),
          };
        }
      );

      // Optimistically update stats (favoriteGames)
      if (previousStats) {
        queryClient.setQueryData<UserLibraryStats>(libraryKeys.stats(), (old) => {
          if (!old) return old;
          return {
            ...old,
            favoriteGames: isFavorite
              ? old.favoriteGames + 1
              : Math.max(0, old.favoriteGames - 1),
          };
        });
      }

      // Optimistically update game status if cached
      if (previousGameStatus) {
        queryClient.setQueryData<GameInLibraryStatus>(
          libraryKeys.gameStatus(gameId),
          (old) => (old ? { ...old, isFavorite } : old)
        );
      }

      return { previousLibrary, previousStats, previousGameStatus };
    },
    onError: (_err, { gameId }, context) => {
      // Rollback to previous data on error
      if (context?.previousLibrary) {
        context.previousLibrary.forEach(([queryKey, data]) => {
          queryClient.setQueryData(queryKey, data);
        });
      }
      if (context?.previousStats) {
        queryClient.setQueryData(libraryKeys.stats(), context.previousStats);
      }
      if (context?.previousGameStatus) {
        queryClient.setQueryData(libraryKeys.gameStatus(gameId), context.previousGameStatus);
      }
    },
    onSettled: (_data, _err, { gameId }) => {
      // Refetch to ensure cache consistency with server
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameStatus(gameId) });
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

// ========================================
// Game State Management (Issue #2868)
// ========================================

/**
 * Hook to update game state (Nuovo/InPrestito/Wishlist/Owned)
 *
 * Updates the state of a single game. Invalidates library cache on success.
 *
 * @returns UseMutationResult for updating game state
 */
export function useUpdateGameState(): UseMutationResult<
  void,
  Error,
  { gameId: string; request: UpdateGameStateRequest }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      gameId,
      request,
    }: {
      gameId: string;
      request: UpdateGameStateRequest;
    }) => {
      return api.library.updateGameState(gameId, request);
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

// ========================================
// Library Sharing Hooks (Issue #2614)
// ========================================

/**
 * Hook to fetch user's current share link
 *
 * Returns the active share link if one exists, or null if none.
 *
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with share link data or null
 */
export function useLibraryShareLink(
  enabled: boolean = true
): UseQueryResult<LibraryShareLink | null, Error> {
  return useQuery({
    queryKey: libraryKeys.shareLink(),
    queryFn: async (): Promise<LibraryShareLink | null> => {
      return api.library.getShareLink();
    },
    enabled,
    staleTime: 5 * 60 * 1000, // Share link unlikely to change frequently (5min)
  });
}

/**
 * Hook to create a new share link
 *
 * Creates a new share link with the specified settings.
 * Any existing active share link will be replaced.
 *
 * @returns UseMutationResult for creating share link
 */
export function useCreateShareLink(): UseMutationResult<
  LibraryShareLink,
  Error,
  CreateLibraryShareLinkRequest
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: CreateLibraryShareLinkRequest) => {
      return api.library.createShareLink(request);
    },
    onSuccess: () => {
      // Invalidate share link query to refetch
      queryClient.invalidateQueries({ queryKey: libraryKeys.shareLink() });
    },
  });
}

/**
 * Hook to update an existing share link
 *
 * Updates share link settings (privacy level, notes inclusion, expiration).
 *
 * @returns UseMutationResult for updating share link
 */
export function useUpdateShareLink(): UseMutationResult<
  LibraryShareLink,
  Error,
  { shareToken: string; request: UpdateLibraryShareLinkRequest }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      shareToken,
      request,
    }: {
      shareToken: string;
      request: UpdateLibraryShareLinkRequest;
    }) => {
      return api.library.updateShareLink(shareToken, request);
    },
    onSuccess: () => {
      // Invalidate share link query to refetch
      queryClient.invalidateQueries({ queryKey: libraryKeys.shareLink() });
    },
  });
}

/**
 * Hook to revoke a share link
 *
 * Permanently disables the share link. The link URL will no longer work.
 *
 * @returns UseMutationResult for revoking share link
 */
export function useRevokeShareLink(): UseMutationResult<void, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (shareToken: string) => {
      return api.library.revokeShareLink(shareToken);
    },
    onSuccess: () => {
      // Invalidate share link query to refetch (will return null after revoke)
      queryClient.invalidateQueries({ queryKey: libraryKeys.shareLink() });
    },
  });
}

/**
 * Hook to fetch a public shared library by share token
 *
 * This is used on the public shared library page to display
 * another user's library. No authentication required.
 *
 * @param shareToken - Share token from URL
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with shared library data or null
 */
export function useSharedLibrary(
  shareToken: string,
  enabled: boolean = true
): UseQueryResult<SharedLibrary | null, Error> {
  return useQuery({
    queryKey: libraryKeys.sharedLibrary(shareToken),
    queryFn: async (): Promise<SharedLibrary | null> => {
      return api.library.getSharedLibrary(shareToken);
    },
    enabled: enabled && !!shareToken,
    staleTime: 2 * 60 * 1000, // Shared library can change (2min)
    retry: false, // Don't retry on 404 (invalid/expired token)
  });
}

// ========================================
// Library Game Detail Hook (Issue #3513)
// ========================================

/**
 * Combined library game detail data
 *
 * Merges UserLibraryEntry (user-specific data) with SharedGameDetail (full game info)
 */
export interface LibraryGameDetail {
  // Library-specific data
  libraryEntryId: string;
  userId: string;
  gameId: string;
  addedAt: string;
  notes: string | null;
  isFavorite: boolean;
  currentState: string;
  stateChangedAt: string | null;
  stateNotes: string | null;
  hasPdfDocuments: boolean;
  // Basic game info from library entry
  gameTitle: string;
  gamePublisher: string | null;
  gameYearPublished: number | null;
  gameIconUrl: string | null;
  gameImageUrl: string | null;
  // Extended game info from SharedGame (optional, may not be loaded)
  description?: string;
  minPlayers?: number;
  maxPlayers?: number;
  playingTimeMinutes?: number;
  minAge?: number;
  complexityRating?: number | null;
  averageRating?: number | null;
  categories?: Array<{ id: string; name: string; slug: string }>;
  mechanics?: Array<{ id: string; name: string; slug: string }>;
  designers?: Array<{ id: string; name: string }>;
  publishers?: Array<{ id: string; name: string }>;
  bggId?: number | null;
}

/**
 * Hook to fetch library game detail for Game Detail page (Issue #3513)
 *
 * This hook combines:
 * 1. UserLibraryEntry - user-specific data (notes, favorite, state)
 * 2. SharedGameDetail - full game information (categories, mechanics, designers)
 *
 * Uses parallel queries for optimal performance.
 *
 * @param gameId - Game UUID (same as SharedGame.id)
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with combined library game detail
 */
export function useLibraryGameDetail(
  gameId: string,
  enabled: boolean = true
): UseQueryResult<LibraryGameDetail | null, Error> {
  return useQuery({
    queryKey: libraryKeys.gameDetail(gameId),
    queryFn: async (): Promise<LibraryGameDetail | null> => {
      // Fetch library entry and shared game detail in parallel
      const [libraryResponse, sharedGame] = await Promise.all([
        api.library.getLibrary({ page: 1, pageSize: 100 }),
        api.sharedGames.getById(gameId),
      ]);

      // Find the library entry for this game
      const libraryEntry = libraryResponse.items.find((item) => item.gameId === gameId);

      if (!libraryEntry) {
        // Game not in user's library
        return null;
      }

      // Combine library entry with shared game detail
      const result: LibraryGameDetail = {
        // Library-specific data
        libraryEntryId: libraryEntry.id,
        userId: libraryEntry.userId,
        gameId: libraryEntry.gameId,
        addedAt: libraryEntry.addedAt,
        notes: libraryEntry.notes ?? null,
        isFavorite: libraryEntry.isFavorite,
        currentState: libraryEntry.currentState,
        stateChangedAt: libraryEntry.stateChangedAt ?? null,
        stateNotes: libraryEntry.stateNotes ?? null,
        hasPdfDocuments: libraryEntry.hasPdfDocuments,
        // Basic game info from library entry
        gameTitle: libraryEntry.gameTitle,
        gamePublisher: libraryEntry.gamePublisher ?? null,
        gameYearPublished: libraryEntry.gameYearPublished ?? null,
        gameIconUrl: libraryEntry.gameIconUrl ?? null,
        gameImageUrl: libraryEntry.gameImageUrl ?? null,
      };

      // Add extended info from SharedGame if available
      if (sharedGame) {
        result.description = sharedGame.description;
        result.minPlayers = sharedGame.minPlayers;
        result.maxPlayers = sharedGame.maxPlayers;
        result.playingTimeMinutes = sharedGame.playingTimeMinutes;
        result.minAge = sharedGame.minAge;
        result.complexityRating = sharedGame.complexityRating;
        result.averageRating = sharedGame.averageRating;
        result.categories = sharedGame.categories;
        result.mechanics = sharedGame.mechanics;
        result.designers = sharedGame.designers;
        result.publishers = sharedGame.publishers;
        result.bggId = sharedGame.bggId;
      }

      return result;
    },
    enabled: enabled && !!gameId,
    staleTime: 2 * 60 * 1000, // Library details can change (2min)
  });
}
