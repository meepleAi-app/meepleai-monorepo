/**
 * Play Records React Query Hooks
 *
 * Type-safe hooks for play records API with optimistic updates and cache management.
 * Issue #3892: Play Records Frontend UI
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { playRecordsApi } from '@/lib/api/play-records.api';
import type {
  PlayRecordDto,
  CreatePlayRecordRequest,
  AddPlayerRequest,
  RecordScoreRequest,
  UpdatePlayRecordRequest,
  ShareLinkResponse,
  PlayRecordVersion,
} from '@/lib/api/schemas/play-records.schemas';

// ========== Types ==========

/**
 * Optional date-range window for player statistics (#2438). Both bounds are
 * ISO-8601 strings; either may be omitted to leave that side unbounded.
 */
export type StatsRange = { startDate?: string; endDate?: string };

// ========== Query Keys ==========

export const playRecordsKeys = {
  all: ['play-records'] as const,
  lists: () => [...playRecordsKeys.all, 'list'] as const,
  list: (filters: Record<string, unknown>) => [...playRecordsKeys.lists(), filters] as const,
  details: () => [...playRecordsKeys.all, 'detail'] as const,
  detail: (id: string) => [...playRecordsKeys.details(), id] as const,
  // Prefix for ALL statistics queries (any range). Mutations invalidate this
  // prefix so every cached range refreshes, not just the no-range entry (#2438).
  statisticsAll: () => [...playRecordsKeys.all, 'statistics'] as const,
  statistics: (range?: StatsRange) =>
    [...playRecordsKeys.statisticsAll(), range?.startDate ?? null, range?.endDate ?? null] as const,
  // #2437-2: public shared-record query (no auth required)
  shared: (token: string) => [...playRecordsKeys.all, 'shared', token] as const,
  // #2437-3: version history for a specific record (creator-only)
  versions: (recordId: string) => [...playRecordsKeys.all, 'versions', recordId] as const,
};

// ========== Queries ==========

/**
 * Fetch full play record details by ID
 */
export function usePlayRecord(id: string | undefined) {
  return useQuery({
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    queryKey: playRecordsKeys.detail(id!),
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    queryFn: () => playRecordsApi.getRecord(id!),
    enabled: !!id,
    retry: false,
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
    retry: false,
  });
}

/**
 * Fetch player statistics across all games
 *
 * AC-5.9: staleTime 5 minutes (300000ms) — stats are relatively stable,
 * allows client-side cache reuse across navigations without refetch.
 */
export function usePlayerStatistics(range?: StatsRange) {
  return useQuery({
    queryKey: playRecordsKeys.statistics(range),
    queryFn: () => playRecordsApi.getPlayerStatistics(range ?? {}),
    staleTime: 5 * 60 * 1000, // 5 minutes
    retry: false,
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
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.statisticsAll() });
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
    onMutate: async newPlayer => {
      // Cancel outgoing queries
      await queryClient.cancelQueries({ queryKey: playRecordsKeys.detail(recordId) });

      // Snapshot previous value
      const previous = queryClient.getQueryData<PlayRecordDto>(playRecordsKeys.detail(recordId));

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
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.statisticsAll() });
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

/**
 * Delete a play record by ID (AC-4.6)
 */
export function useDeleteRecord(recordId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => playRecordsApi.deleteRecord(recordId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.detail(recordId) });
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.lists() });
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.statisticsAll() });
    },
  });
}

// ========== Share Token Hooks (#2437-2) ==========

/**
 * Generate (or rotate) a share token for a play record.
 * Creator-only. Invalidates the detail cache so shareToken is refreshed.
 */
export function useGeneratePlayRecordShareToken(recordId: string) {
  const queryClient = useQueryClient();

  return useMutation<ShareLinkResponse, Error>({
    mutationFn: () => playRecordsApi.generateShareToken(recordId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.detail(recordId) });
    },
  });
}

/**
 * Revoke the share token for a play record.
 * Creator-only. Invalidates the detail cache so shareToken clears.
 */
export function useRevokePlayRecordShareToken(recordId: string) {
  const queryClient = useQueryClient();

  return useMutation<void, Error>({
    mutationFn: () => playRecordsApi.revokeShareToken(recordId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.detail(recordId) });
    },
  });
}

/**
 * Fetch a publicly shared play record by its share token (no auth required).
 * Disabled when token is empty. retry: false — 404 is a terminal state.
 */
export function useSharedPlayRecord(token: string) {
  return useQuery<PlayRecordDto, Error>({
    queryKey: playRecordsKeys.shared(token),
    queryFn: () => playRecordsApi.getSharedRecord(token),
    enabled: !!token,
    retry: false,
  });
}

// ========== Version History Hooks (#2437-3) ==========

/**
 * Fetch the last-5 audit versions for a play record (creator-only).
 * `enabled` can be set to false to suppress the query until needed (e.g. lazy dialog).
 */
export function usePlayRecordVersions(recordId: string, enabled = true) {
  return useQuery<PlayRecordVersion[], Error>({
    queryKey: playRecordsKeys.versions(recordId),
    queryFn: () => playRecordsApi.getVersions(recordId),
    enabled,
    retry: false,
  });
}

/**
 * Restore a play record to a specific version (creator-only).
 * On success invalidates both the record detail and its version list so both
 * surfaces reflect the restored state.
 */
export function useRestorePlayRecordVersion(recordId: string) {
  const queryClient = useQueryClient();

  return useMutation<void, Error, number>({
    mutationFn: (versionNumber: number) => playRecordsApi.restoreVersion(recordId, versionNumber),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.detail(recordId) });
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.versions(recordId) });
    },
  });
}
