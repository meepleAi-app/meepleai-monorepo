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

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { api } from '@/lib/api';
import { fetchUserGamebooks } from '@/lib/api/gamebooks-list';
import type { LibraryActivityItem } from '@/lib/api/schemas/library-activity.schemas';
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
import type {
  PrivateGameDto,
  AddPrivateGameRequest,
} from '@/lib/api/schemas/private-games.schemas';

import { sharedGamesKeys } from './useSharedGames';

/**
 * Query key factory for library queries
 */
export const libraryKeys = {
  all: ['library'] as const,
  lists: () => [...libraryKeys.all, 'list'] as const,
  list: (params?: GetUserLibraryParams) => [...libraryKeys.lists(), { params }] as const,
  stats: () => [...libraryKeys.all, 'stats'] as const,
  quota: () => [...libraryKeys.all, 'quota'] as const,
  activity: (limit: number) => [...libraryKeys.all, 'activity', { limit }] as const,
  gameStatus: (gameId: string) => [...libraryKeys.all, 'status', gameId] as const,
  // Game detail key (Issue #3513)
  gameDetail: (gameId: string) => [...libraryKeys.all, 'detail', gameId] as const,
  // Share link keys (Issue #2614)
  shareLink: () => [...libraryKeys.all, 'shareLink'] as const,
  sharedLibrary: (shareToken: string) => [...libraryKeys.all, 'shared', shareToken] as const,
  // Private game keys (Game Night Flow)
  privateGame: (id: string) => [...libraryKeys.all, 'privateGame', id] as const,
  privateGames: (params?: Record<string, unknown>) =>
    [...libraryKeys.all, 'privateGames', { params }] as const,
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
  const { data: user } = useCurrentUser();
  const isAuthenticated = !!user;

  return useQuery({
    queryKey: libraryKeys.quota(),
    queryFn: async (): Promise<LibraryQuotaResponse> => {
      return api.library.getQuota();
    },
    enabled: enabled && isAuthenticated,
    staleTime: 5 * 60 * 1000, // Quota unlikely to change frequently (5min)
  });
}

/**
 * Hook to fetch the user's library activity feed (Issue #642 — Wave B.3 followup).
 *
 * Powers the `RecentActivityRail` sidebar on `/library`. Server-side limit is
 * clamped to 1–50; default 20.
 *
 * @param limit - Number of recent events to fetch (default: 20)
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with the activity feed
 */
export function useLibraryActivity(
  limit: number = 20,
  enabled: boolean = true
): UseQueryResult<LibraryActivityItem[], Error> {
  return useQuery({
    queryKey: libraryKeys.activity(limit),
    queryFn: async (): Promise<LibraryActivityItem[]> => {
      return api.library.getActivity(limit);
    },
    enabled,
    staleTime: 1 * 60 * 1000, // Activity feed refreshes every minute
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
  const { data: user } = useCurrentUser();
  const isAuthenticated = !!user;

  return useQuery({
    queryKey: libraryKeys.gameStatus(gameId),
    queryFn: async (): Promise<GameInLibraryStatus> => {
      return api.library.getGameStatus(gameId);
    },
    enabled: enabled && !!gameId && isAuthenticated,
    staleTime: 30 * 1000, // Status can change frequently (30s)

    // Flickering fixes (Research: TanStack Query best practices)
    retryOnMount: false, // Prevent retry loop on component mount/unmount cycles
    structuralSharing: false, // Disable for simple status objects (performance)
    notifyOnChangeProps: ['data', 'error'], // Only re-render on data/error changes (not isFetching)
    retry: 1, // Reduce from 3 to 1 (minimize flickering on real failures)
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
      // Cross-cache: also cancel useCollectionActions query
      await queryClient.cancelQueries({ queryKey: ['library-status', gameId] });

      // Snapshot previous values for rollback
      const previousGameStatus = queryClient.getQueryData<GameInLibraryStatus>(
        libraryKeys.gameStatus(gameId)
      );
      const previousQuota = queryClient.getQueryData<LibraryQuotaResponse>(libraryKeys.quota());
      const previousCollectionStatus = queryClient.getQueryData(['library-status', gameId]);

      // Optimistically update game status to 'InLibrary'
      queryClient.setQueryData<GameInLibraryStatus>(libraryKeys.gameStatus(gameId), old =>
        old ? { ...old, inLibrary: true } : { inLibrary: true, isFavorite: false }
      );

      // Cross-cache: also update useCollectionActions cache for immediate MeepleCard sync
      queryClient.setQueryData(
        ['library-status', gameId],
        (old: Record<string, unknown> | undefined) =>
          old
            ? { ...old, inLibrary: true }
            : { inLibrary: true, isFavorite: false, associatedData: null }
      );

      // Optimistically update quota
      if (previousQuota) {
        queryClient.setQueryData<LibraryQuotaResponse>(libraryKeys.quota(), old => {
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

      return { previousGameStatus, previousQuota, previousCollectionStatus };
    },
    onError: (_err, { gameId }, context) => {
      // Rollback to previous data on error
      if (context?.previousGameStatus) {
        queryClient.setQueryData(libraryKeys.gameStatus(gameId), context.previousGameStatus);
      }
      if (context?.previousQuota) {
        queryClient.setQueryData(libraryKeys.quota(), context.previousQuota);
      }
      if (context?.previousCollectionStatus) {
        queryClient.setQueryData(['library-status', gameId], context.previousCollectionStatus);
      }
    },
    onSettled: (_data, _err, { gameId }) => {
      // Refetch to ensure cache consistency with server
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.quota() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameStatus(gameId) });

      // Cross-cache: invalidate useCollectionActions cache
      queryClient.invalidateQueries({ queryKey: ['library-status', gameId] });

      // Issue #4: Invalidate game search cache to show updated library status
      queryClient.invalidateQueries({ queryKey: ['games', 'search'] });
      queryClient.invalidateQueries({ queryKey: ['wishlist'] });

      // Issue #1: Invalidate tag/genre filters sidebar (CatalogFilters)
      queryClient.invalidateQueries({ queryKey: sharedGamesKeys.categories() });
      queryClient.invalidateQueries({ queryKey: sharedGamesKeys.mechanics() });
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
    onMutate: async gameId => {
      // Cancel any outgoing refetches to avoid overwriting optimistic update
      await queryClient.cancelQueries({ queryKey: libraryKeys.lists() });
      await queryClient.cancelQueries({ queryKey: libraryKeys.gameStatus(gameId) });
      await queryClient.cancelQueries({ queryKey: libraryKeys.quota() });
      // Cross-cache: also cancel useCollectionActions query
      await queryClient.cancelQueries({ queryKey: ['library-status', gameId] });

      // Snapshot previous values for rollback
      const previousLibrary = queryClient.getQueriesData<PaginatedLibraryResponse>({
        queryKey: libraryKeys.lists(),
      });
      const previousGameStatus = queryClient.getQueryData<GameInLibraryStatus>(
        libraryKeys.gameStatus(gameId)
      );
      const previousQuota = queryClient.getQueryData<LibraryQuotaResponse>(libraryKeys.quota());
      const previousStats = queryClient.getQueryData<UserLibraryStats>(libraryKeys.stats());
      const previousCollectionStatus = queryClient.getQueryData(['library-status', gameId]);

      // Optimistically remove game from library lists
      queryClient.setQueriesData<PaginatedLibraryResponse>(
        { queryKey: libraryKeys.lists() },
        old => {
          if (!old) return old;
          return {
            ...old,
            items: old.items.filter(entry => entry.gameId !== gameId),
            totalCount: Math.max(0, old.totalCount - 1),
          };
        }
      );

      // Optimistically update game status to not in library
      queryClient.setQueryData<GameInLibraryStatus>(libraryKeys.gameStatus(gameId), old =>
        old
          ? { ...old, inLibrary: false, isFavorite: false }
          : { inLibrary: false, isFavorite: false }
      );

      // Cross-cache: also update useCollectionActions cache for immediate MeepleCard sync
      queryClient.setQueryData(
        ['library-status', gameId],
        (old: Record<string, unknown> | undefined) =>
          old
            ? { ...old, inLibrary: false, isFavorite: false }
            : { inLibrary: false, isFavorite: false, associatedData: null }
      );

      // Optimistically update quota
      if (previousQuota) {
        queryClient.setQueryData<LibraryQuotaResponse>(libraryKeys.quota(), old => {
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
        queryClient.setQueryData<UserLibraryStats>(libraryKeys.stats(), old => {
          if (!old) return old;
          return {
            ...old,
            totalGames: Math.max(0, old.totalGames - 1),
          };
        });
      }

      return {
        previousLibrary,
        previousGameStatus,
        previousQuota,
        previousStats,
        previousCollectionStatus,
      };
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
      if (context?.previousCollectionStatus) {
        queryClient.setQueryData(['library-status', gameId], context.previousCollectionStatus);
      }
    },
    onSettled: (_data, _err, gameId) => {
      // Refetch to ensure cache consistency with server
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.quota() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameStatus(gameId) });

      // Cross-cache: invalidate useCollectionActions cache
      queryClient.invalidateQueries({ queryKey: ['library-status', gameId] });

      // Issue #4: Invalidate game search cache to show updated library status
      queryClient.invalidateQueries({ queryKey: ['games', 'search'] });
      queryClient.invalidateQueries({ queryKey: ['wishlist'] });

      // Issue #1: Invalidate tag/genre filters sidebar (CatalogFilters)
      queryClient.invalidateQueries({ queryKey: sharedGamesKeys.categories() });
      queryClient.invalidateQueries({ queryKey: sharedGamesKeys.mechanics() });
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
        old => {
          if (!old) return old;
          return {
            ...old,
            items: old.items.map(entry =>
              entry.gameId === gameId ? { ...entry, isFavorite } : entry
            ),
          };
        }
      );

      // Optimistically update stats (favoriteGames)
      if (previousStats) {
        queryClient.setQueryData<UserLibraryStats>(libraryKeys.stats(), old => {
          if (!old) return old;
          return {
            ...old,
            favoriteGames: isFavorite ? old.favoriteGames + 1 : Math.max(0, old.favoriteGames - 1),
          };
        });
      }

      // Optimistically update game status if cached
      if (previousGameStatus) {
        queryClient.setQueryData<GameInLibraryStatus>(libraryKeys.gameStatus(gameId), old =>
          old ? { ...old, isFavorite } : old
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
 * Combined library game detail for Game Detail page (Issue #3513)
 *
 * Uses the backend's GET /library/{gameId} endpoint for efficient
 * single-request data fetching with all metadata and play statistics.
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
  isAvailableForPlay: boolean;
  hasCustomPdf: boolean;
  hasRagAccess: boolean;
  // Issue #1288 Bug 2 — counter aggregati e KB status derivato.
  // Popolati via merge con `/api/v1/gamebooks` quando la library entry
  // qualifica come gamebook (active campaign OR private PDF). Zero/null
  // negli altri casi.
  chunkCount?: number;
  sessionsCount?: number;
  kbStatus?: 'ready' | 'indexing' | 'error';
  // Game metadata
  gameTitle: string;
  gamePublisher: string | null;
  gameYearPublished: number | null;
  gameIconUrl: string | null;
  gameImageUrl: string | null;
  description: string | null;
  minPlayers: number | null;
  maxPlayers: number | null;
  playingTimeMinutes: number | null;
  minAge?: number;
  complexityRating: number | null;
  averageRating: number | null;
  // Play statistics (from GameDetailDto)
  timesPlayed: number;
  lastPlayed: string | null;
  winRate: string | null;
  avgDuration: string | null;
  // Extended data (from SharedGame via parallel fetch)
  categories?: Array<{ id: string; name: string; slug: string }>;
  mechanics?: Array<{ id: string; name: string; slug: string }>;
  designers?: Array<{ id: string; name: string }>;
  publishers?: Array<{ id: string; name: string }>;
  bggId?: number | null;
  // Recent sessions (if loaded)
  recentSessions?: Array<{
    id: string;
    playedAt: string;
    durationMinutes: number;
    durationFormatted: string;
    didWin: boolean | null;
    players: string | null;
    notes: string | null;
  }>;
}

/**
 * Map a PrivateGameDto into the LibraryGameDetail shape so the unified
 * `/library/[gameId]` route can render private-game ownership (Issue #1068).
 *
 * Private games are user-owned game records not present in the shared catalog —
 * typically dogfood titles (Nanolith) or manually-added entries. They expose a
 * narrower schema than GameDetailDto (no play stats, no shared metadata), so
 * the mapper substitutes sentinel/null values for fields the source cannot
 * provide. `libraryEntryId === ''` is the same sentinel used by the catalog
 * fallback to signal `heroVariant === 'community'` downstream.
 */
function mapPrivateGameToLibraryGameDetail(privateGame: PrivateGameDto): LibraryGameDetail {
  return {
    libraryEntryId: '',
    userId: privateGame.ownerId,
    gameId: privateGame.id,
    addedAt: privateGame.createdAt,
    notes: null,
    isFavorite: false,
    currentState: 'Nuovo',
    stateChangedAt: null,
    stateNotes: null,
    isAvailableForPlay: true,
    hasCustomPdf: false,
    hasRagAccess: false,
    gameTitle: privateGame.title,
    gamePublisher: null,
    gameYearPublished: privateGame.yearPublished ?? null,
    gameIconUrl: privateGame.thumbnailUrl ?? null,
    gameImageUrl: privateGame.imageUrl ?? null,
    description: privateGame.description ?? null,
    minPlayers: privateGame.minPlayers,
    maxPlayers: privateGame.maxPlayers,
    playingTimeMinutes: privateGame.playingTimeMinutes ?? null,
    minAge: privateGame.minAge ?? undefined,
    complexityRating: privateGame.complexityRating ?? null,
    averageRating: null,
    timesPlayed: 0,
    lastPlayed: null,
    winRate: null,
    avgDuration: null,
    bggId: privateGame.bggId ?? null,
  };
}

/**
 * Hook to fetch library game detail for Game Detail page (Issue #3513)
 *
 * Uses the efficient GET /library/{gameId} endpoint that returns
 * all game metadata and play statistics in a single request.
 *
 * For extended data (categories, mechanics, designers), fetches SharedGame in parallel.
 *
 * Issue #1068: also probes PrivateGame as third source so private-game ownership
 * (Nanolith dogfood) renders correctly on the unified `/library/[gameId]` route
 * introduced by PR #1037 IA consolidation.
 *
 * @param gameId - Game UUID (SharedGame.id, PrivateGame.id, or library entry UUID)
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
      // Issue #1068 (WS-B Mockup Conformity): probe three sources in parallel.
      // After PR #1037 IA consolidation collapsed `/library/games/[id]` →
      // `/library/[id]`, the same URL must serve shared-library entries,
      // shared-catalog browse, AND private-game ownership (e.g. Nanolith
      // dogfood). Without the PrivateGame fallback, page.tsx:76 collapses
      // owned private games into a misleading "Gioco non trovato" state.
      const [gameDetail, sharedGame, privateGame, gamebooks] = await Promise.all([
        api.library.getGameDetail(gameId).catch(() => null),
        api.sharedGames.getById(gameId).catch(() => null),
        api.library.getPrivateGame(gameId).catch(() => null),
        // Issue #1288 Bug 2 — merge counter aggregati dal nuovo endpoint
        // /api/v1/gamebooks (popolato solo per entry con campagna attiva o
        // PDF privato). Catch → null per giochi senza qualifica gamebook.
        fetchUserGamebooks().catch(() => [] as const),
      ]);

      // Cerca lo stesso gameId nell'elenco gamebooks per estrarre i counter
      // reali (chunks, sessionsCount, kbStatus).
      const gamebookMatch = gamebooks.find(g => g.gameId === gameId);
      const gamebookStats = gamebookMatch
        ? {
            chunkCount: gamebookMatch.chunks,
            sessionsCount: gamebookMatch.sessionsCount,
            kbStatus: (gamebookMatch.status === 'indexing' || gamebookMatch.status === 'error'
              ? gamebookMatch.status
              : 'ready') as 'ready' | 'indexing' | 'error',
          }
        : null;

      if (!gameDetail) {
        // PrivateGame ownership takes priority over catalog fallback: an owned
        // private game is more specific than a community-only entry, and
        // typically a private-game UUID will NOT match shared_games anyway.
        if (privateGame) {
          const mapped = mapPrivateGameToLibraryGameDetail(privateGame);
          return gamebookStats ? { ...mapped, ...gamebookStats } : mapped;
        }
        // Game not in user's library → fall back to catalog-only view (G1).
        // The detail page must render for ANY shared_game so the user can
        // see metadata and click "Aggiungi alla libreria". The sentinel
        // libraryEntryId === '' signals heroVariant === 'community' downstream
        // (GameDetailView uses truthy check on libraryEntryId).
        if (!sharedGame) {
          // Genuine not-found: neither library entry nor catalog record.
          return null;
        }
        return {
          libraryEntryId: '',
          userId: '',
          gameId: sharedGame.id,
          addedAt: '',
          notes: null,
          isFavorite: false,
          currentState: 'Nuovo',
          stateChangedAt: null,
          stateNotes: null,
          isAvailableForPlay: false,
          hasCustomPdf: false,
          hasRagAccess: false,
          gameTitle: sharedGame.title,
          gamePublisher: sharedGame.publishers?.[0]?.name ?? null,
          gameYearPublished: sharedGame.yearPublished ?? null,
          gameIconUrl: sharedGame.thumbnailUrl ?? null,
          gameImageUrl: sharedGame.imageUrl ?? null,
          description: sharedGame.description ?? null,
          minPlayers: sharedGame.minPlayers ?? null,
          maxPlayers: sharedGame.maxPlayers ?? null,
          playingTimeMinutes: sharedGame.playingTimeMinutes ?? null,
          minAge: sharedGame.minAge,
          complexityRating: sharedGame.complexityRating ?? null,
          averageRating: sharedGame.averageRating ?? null,
          timesPlayed: 0,
          lastPlayed: null,
          winRate: null,
          avgDuration: null,
          categories: sharedGame.categories,
          mechanics: sharedGame.mechanics,
          designers: sharedGame.designers,
          publishers: sharedGame.publishers,
          bggId: sharedGame.bggId,
          ...(gamebookStats ?? {}),
        };
      }

      // Map GameDetailDto to LibraryGameDetail
      const result: LibraryGameDetail = {
        // Library-specific data
        libraryEntryId: gameDetail.id,
        userId: gameDetail.userId,
        gameId: gameDetail.gameId,
        addedAt: gameDetail.addedAt,
        notes: gameDetail.notes,
        isFavorite: gameDetail.isFavorite,
        currentState: gameDetail.currentState,
        stateChangedAt: gameDetail.stateChangedAt,
        stateNotes: gameDetail.stateNotes,
        isAvailableForPlay: gameDetail.isAvailableForPlay,
        hasCustomPdf: !!gameDetail.customPdf,
        hasRagAccess: gameDetail.hasRagAccess ?? false,
        // Game metadata
        gameTitle: gameDetail.gameTitle,
        gamePublisher: gameDetail.gamePublisher,
        gameYearPublished: gameDetail.gameYearPublished,
        gameIconUrl: gameDetail.gameIconUrl,
        gameImageUrl: gameDetail.gameImageUrl,
        description: gameDetail.gameDescription,
        minPlayers: gameDetail.minPlayers,
        maxPlayers: gameDetail.maxPlayers,
        playingTimeMinutes: gameDetail.playTimeMinutes,
        complexityRating: gameDetail.complexityRating,
        averageRating: gameDetail.averageRating,
        // Play statistics
        timesPlayed: gameDetail.timesPlayed,
        lastPlayed: gameDetail.lastPlayed,
        winRate: gameDetail.winRate,
        avgDuration: gameDetail.avgDuration,
        // Recent sessions
        recentSessions: gameDetail.recentSessions ?? undefined,
      };

      // Add extended info from SharedGame if available (categories, mechanics, designers)
      if (sharedGame) {
        result.minAge = sharedGame.minAge;
        result.categories = sharedGame.categories;
        result.mechanics = sharedGame.mechanics;
        result.designers = sharedGame.designers;
        result.publishers = sharedGame.publishers;
        result.bggId = sharedGame.bggId;
      }

      // Issue #1288 Bug 2 — merge counter aggregati gamebook se questa
      // library entry qualifica come gamebook (active campaign OR private PDF).
      if (gamebookStats) {
        result.chunkCount = gamebookStats.chunkCount;
        result.sessionsCount = gamebookStats.sessionsCount;
        result.kbStatus = gamebookStats.kbStatus;
      }

      return result;
    },
    enabled: enabled && !!gameId,
    staleTime: 2 * 60 * 1000, // Library details can change (2min)
  });
}

// ========================================
// Private Game Hooks (Game Night Flow)
// ========================================

/**
 * Hook to fetch a single private game by ID
 *
 * @param privateGameId - Private game UUID
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with private game data
 */
export function usePrivateGame(
  privateGameId: string | undefined,
  enabled = true
): UseQueryResult<PrivateGameDto, Error> {
  return useQuery({
    queryKey: libraryKeys.privateGame(privateGameId ?? ''),
    queryFn: async (): Promise<PrivateGameDto> => {
      return api.library.getPrivateGame(privateGameId!);
    },
    enabled: enabled && !!privateGameId,
    staleTime: 2 * 60 * 1000,
  });
}

/**
 * Hook to add a private game from BGG and automatically add it to the library
 *
 * Creates the private game entry, then adds it to the user's library as Owned.
 * Invalidates all library caches on success.
 *
 * @returns UseMutationResult for adding private game from BGG
 */
export function useAddPrivateGameFromBgg(): UseMutationResult<
  PrivateGameDto,
  Error,
  AddPrivateGameRequest
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: AddPrivateGameRequest): Promise<PrivateGameDto> => {
      const privateGame = await api.library.addPrivateGame(request);
      await api.library.addGame(privateGame.id, { isFavorite: false });
      return privateGame;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: libraryKeys.all });
    },
  });
}
