/**
 * Play Records React Query Hooks
 *
 * Type-safe hooks for play records API with optimistic updates and cache management.
 * Issue #3892: Play Records Frontend UI
 */

import { useMutation, useQuery, useQueryClient, useInfiniteQuery } from '@tanstack/react-query';
import { playRecordsApi } from '@/lib/api/play-records.api';
import type {
  PlayRecordDto,
  PlayHistoryResponse,
  PlayerStatistics,
  CreatePlayRecordRequest,
  AddPlayerRequest,
  RecordScoreRequest,
  UpdatePlayRecordRequest,
} from '@/lib/api/schemas/play-records.schemas';

// ========== Query Keys ==========

export const playRecordsKeys = {
  all: ['play-records'] as const,
  lists: () => [...playRecordsKeys.all, 'list'] as const,
  list: (filters: Record<string, unknown>) => [...playRecordsKeys.lists(), filters] as const,
  details: () => [...playRecordsKeys.all, 'detail'] as const,
  detail: (id: string) => [...playRecordsKeys.details(), id] as const,
  statistics: () => [...playRecordsKeys.all, 'statistics'] as const,
};

// ========== Queries ==========

/**
 * Fetch full play record details by ID
 */
export function usePlayRecord(id: string | undefined) {
  return useQuery({
    queryKey: playRecordsKeys.detail(id!),
    queryFn: () => playRecordsApi.getRecord(id!),
    enabled: !!id,
  });
}

/**
 * Fetch user's play history with filters and pagination
 */
export function usePlayHistory(params: {
  page?: number;
  pageSize?: number;
  gameId?: string;
  status?: string;
  dateFrom?: string;
  dateTo?: string;
}) {
  return useQuery({
    queryKey: playRecordsKeys.list(params),
    queryFn: () => playRecordsApi.getUserHistory(params),
  });
}

/**
 * Infinite query for play history (scroll pagination)
 */
export function useInfinitePlayHistory(params: {
  pageSize?: number;
  gameId?: string;
  status?: string;
  dateFrom?: string;
  dateTo?: string;
}) {
  return useInfiniteQuery({
    queryKey: playRecordsKeys.list(params),
    queryFn: ({ pageParam = 1 }) =>
      playRecordsApi.getUserHistory({
        ...params,
        page: pageParam,
      }),
    initialPageParam: 1,
    getNextPageParam: (lastPage: PlayHistoryResponse) => {
      if (lastPage.page < lastPage.totalPages) {
        return lastPage.page + 1;
      }
      return undefined;
    },
  });
}

/**
 * Fetch player statistics across all games
 */
export function usePlayerStatistics() {
  return useQuery({
    queryKey: playRecordsKeys.statistics(),
    queryFn: () => playRecordsApi.getPlayerStatistics(),
  });
}

// ========== Mutations ==========

/**
 * Create a new play record
 */
export function useCreatePlayRecord() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreatePlayRecordRequest) => playRecordsApi.createRecord(data),
    onSuccess: () => {
      // Invalidate history to show new record
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.lists() });
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.statistics() });
    },
  });
}

/**
 * Add a player to a play record
 */
export function useAddPlayer(recordId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (player: AddPlayerRequest) => playRecordsApi.addPlayer(recordId, player),
    onMutate: async (newPlayer) => {
      // Cancel outgoing queries
      await queryClient.cancelQueries({ queryKey: playRecordsKeys.detail(recordId) });

      // Snapshot previous value
      const previous = queryClient.getQueryData<PlayRecordDto>(
        playRecordsKeys.detail(recordId)
      );

      // Optimistically update
      if (previous) {
        queryClient.setQueryData<PlayRecordDto>(playRecordsKeys.detail(recordId), {
          ...previous,
          players: [
            ...previous.players,
            {
              id: crypto.randomUUID(), // Temporary ID
              userId: newPlayer.userId ?? null,
              displayName: newPlayer.displayName,
              scores: [],
            },
          ],
        });
      }

      return { previous };
    },
    onError: (err, newPlayer, context) => {
      // Rollback on error
      if (context?.previous) {
        queryClient.setQueryData(playRecordsKeys.detail(recordId), context.previous);
      }
    },
    onSettled: () => {
      // Refetch to get server state
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.detail(recordId) });
    },
  });
}

/**
 * Record a score for a player
 */
export function useRecordScore(recordId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (score: RecordScoreRequest) => playRecordsApi.recordScore(recordId, score),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.detail(recordId) });
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.statistics() });
    },
  });
}

/**
 * Start a play record (mark as InProgress)
 */
export function useStartRecord(recordId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => playRecordsApi.startRecord(recordId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.detail(recordId) });
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.lists() });
    },
  });
}

/**
 * Complete a play record
 */
export function useCompleteRecord(recordId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => playRecordsApi.completeRecord(recordId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.detail(recordId) });
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.lists() });
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.statistics() });
    },
  });
}

/**
 * Update play record details
 */
export function useUpdateRecord(recordId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (updates: UpdatePlayRecordRequest) =>
      playRecordsApi.updateRecord(recordId, updates),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.detail(recordId) });
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.lists() });
    },
  });
}