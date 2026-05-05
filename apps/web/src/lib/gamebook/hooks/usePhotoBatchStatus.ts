/**
 * Gamebook — usePhotoBatchStatus hook (Sprint 1, Task 1.8)
 *
 * TanStack Query v5 polling hook for photo batch status.
 * Polls every 2 s while status is non-terminal; stops automatically
 * once status ∈ {Completed, Failed, Cancelled}.
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { getPhotoBatchStatus } from '../api';
import { isBatchTerminal, type PhotoBatchStatus } from '../schemas';

const POLL_INTERVAL_MS = 2_000;

/**
 * Query key factory for gamebook batch status.
 * Exported so test files can invalidate or prime the cache.
 */
export const photoBatchStatusKeys = {
  byBatch: (gameId: string, batchId: string) =>
    ['gamebook', 'photoBatch', 'status', gameId, batchId] as const,
};

/**
 * Polling hook for a photo batch processing status.
 *
 * - Disabled when `batchId` is null (no fetch fires).
 * - Polls every 2 s while status is pending / processing.
 * - Stops polling on terminal status (Completed | Failed | Cancelled).
 *
 * @example
 * ```tsx
 * const { data: status } = usePhotoBatchStatus({ gameId, batchId });
 * ```
 */
export function usePhotoBatchStatus({
  gameId,
  batchId,
}: {
  gameId: string;
  batchId: string | null;
}): UseQueryResult<PhotoBatchStatus | null, Error> {
  return useQuery({
    queryKey: photoBatchStatusKeys.byBatch(gameId, batchId ?? ''),
    queryFn: () => getPhotoBatchStatus(gameId, batchId as string),
    enabled: !!batchId,
    refetchInterval: query => {
      const status = query.state.data?.status;
      if (status && isBatchTerminal(status)) {
        return false;
      }
      return POLL_INTERVAL_MS;
    },
    // Don't retry on 404/401 — polling will resume normally on next interval
    retry: false,
  });
}
