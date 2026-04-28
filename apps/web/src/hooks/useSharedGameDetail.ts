/**
 * useSharedGameDetail — TanStack Query hook for /shared-games/[id] detail.
 *
 * Wave A.4 (Issue #603). Wraps `getSharedGameDetail()` from
 * `lib/api/shared-games.ts` with a stable query key and SSR seed support.
 *
 * Contract (mirrors A.3b's `useSharedGames` pattern):
 *  - `queryKey: ['shared-game-detail', id]` — id is sufficient for stability
 *    (no filters / sort to encode).
 *  - `initialData` from SSR seeds the cache; `staleTime: 60_000` aligns with
 *    backend HybridCache TTL (1 min, see Wave A.3a) → no immediate background
 *    refetch on mount when SSR succeeded.
 *  - `enabled: id.length > 0` defends against router transitions where `id`
 *    is briefly empty.
 */

'use client';

import { useQuery } from '@tanstack/react-query';

import { getSharedGameDetail, type SharedGameDetailV2 } from '@/lib/api/shared-games';

const STALE_MS = 60_000;

export interface UseSharedGameDetailArgs {
  readonly id: string;
  /** SSR seed; passed through to React Query as `initialData`. */
  readonly initialData?: SharedGameDetailV2;
}

export interface UseSharedGameDetailResult {
  readonly data: SharedGameDetailV2 | undefined;
  readonly isLoading: boolean;
  readonly isFetching: boolean;
  readonly isError: boolean;
  readonly error: Error | null;
  readonly refetch: () => Promise<void>;
}

export function useSharedGameDetail(args: UseSharedGameDetailArgs): UseSharedGameDetailResult {
  const queryKey = ['shared-game-detail', args.id] as const;

  const result = useQuery<SharedGameDetailV2, Error>({
    queryKey,
    queryFn: () => getSharedGameDetail(args.id),
    initialData: args.initialData,
    staleTime: STALE_MS,
    enabled: args.id.length > 0,
  });

  const refetch = async (): Promise<void> => {
    await result.refetch();
  };

  return {
    data: result.data,
    isLoading: result.isLoading,
    isFetching: result.isFetching,
    isError: result.isError,
    error: result.error,
    refetch,
  };
}
