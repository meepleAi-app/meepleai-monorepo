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
 *
 * Issue #615 — FSM status: the hook collapses TanStack Query's flag soup
 * (`isLoading | isFetching | isError | data`) plus the typed error contract
 * from `SharedGamesApiError` into a single `status` value the UI can switch
 * on. The five terminal states model what the page can actually render —
 * `'empty'` is derived from data shape, `'not-found'` requires an HTTP 404.
 */

'use client';

import { useQuery } from '@tanstack/react-query';

import {
  SharedGamesApiError,
  getSharedGameDetail,
  type SharedGameDetailV2,
} from '@/lib/api/shared-games';

const STALE_MS = 60_000;

/**
 * Finite-state machine for the detail page surface. Mutually exclusive —
 * exactly one applies at any time.
 *
 *  - `loading`   — first fetch in flight, no SSR seed.
 *  - `default`   — data present and at least one of toolkits/agents/kbs is non-empty.
 *  - `empty`     — data present but `toolkitsCount === 0 && agentsCount === 0 && kbsCount === 0`.
 *  - `not-found` — backend returned HTTP 404 (game does not exist or is unpublished).
 *  - `error`     — any other failure: 5xx, network, timeout, schema mismatch.
 */
export type SharedGameDetailFsmStatus = 'loading' | 'default' | 'empty' | 'not-found' | 'error';

export interface UseSharedGameDetailArgs {
  readonly id: string;
  /** SSR seed; passed through to React Query as `initialData`. */
  readonly initialData?: SharedGameDetailV2;
}

export interface UseSharedGameDetailResult {
  readonly data: SharedGameDetailV2 | undefined;
  readonly status: SharedGameDetailFsmStatus;
  readonly isLoading: boolean;
  readonly isFetching: boolean;
  readonly isError: boolean;
  readonly error: Error | null;
  readonly refetch: () => Promise<void>;
}

/**
 * Decide whether a successful detail payload is "empty" — no toolkits,
 * no agents, no KBs. Matches the spec's `'empty'` UI state, distinct from
 * `'not-found'`: the game exists, but nobody has shared anything yet.
 */
function isDetailEmpty(detail: SharedGameDetailV2): boolean {
  return detail.toolkitsCount === 0 && detail.agentsCount === 0 && detail.kbsCount === 0;
}

/**
 * Map TanStack Query's status surface + our typed error contract to the
 * five-state FSM. Order matters: error first (so an error during a refetch
 * over stale data still surfaces), then data-present branches, then
 * loading.
 */
function deriveFsmStatus(
  data: SharedGameDetailV2 | undefined,
  error: Error | null,
  isLoading: boolean
): SharedGameDetailFsmStatus {
  if (error) {
    if (error instanceof SharedGamesApiError && error.kind === 'http' && error.httpStatus === 404) {
      return 'not-found';
    }
    return 'error';
  }
  if (data) {
    return isDetailEmpty(data) ? 'empty' : 'default';
  }
  if (isLoading) {
    return 'loading';
  }
  // Disabled query (id === '') or unfetched — treat as loading from the
  // UI's perspective; pages that legitimately render with no id won't
  // mount this hook.
  return 'loading';
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

  const status = deriveFsmStatus(result.data, result.error, result.isLoading);

  const refetch = async (): Promise<void> => {
    await result.refetch();
  };

  return {
    data: result.data,
    status,
    isLoading: result.isLoading,
    isFetching: result.isFetching,
    isError: result.isError,
    error: result.error,
    refetch,
  };
}
