/**
 * useOptimisticMutation — generic optimistic-update hook (Issue #951 AC-H2, scope note 2).
 *
 * Why a new generic primitive:
 *   - No prior optimistic-update pattern in this codebase (verified 2026-05-15:
 *     grep `useOptimistic|optimisticUpdate` returned zero matches outside this file).
 *   - Issue #951 RSVP flow needs optimistic feedback < 100ms after click while the
 *     backend roundtrip may take up to 8s on degraded networks.
 *   - Future RSVP-shaped flows (house rules accept, agent install, guest claim)
 *     will reuse this primitive, so paying the foundation debt atomically here.
 *
 * Design:
 *   1. Caller provides a React Query cache key and an `applyOptimistic` reducer
 *      `(cachedValue, variables) => optimisticValue`.
 *   2. On mutate() we snapshot the cache, apply the reducer, then run the network call.
 *   3. On success we invalidate (server is truth).
 *   4. On error we restore the snapshot and surface the error via React Query's
 *      reactive `mutation.error`.
 *
 * Composition over React Query's built-in `onMutate`:
 *   The built-in pattern requires hand-rolling snapshot/restore in every consumer.
 *   This wrapper enforces the snapshot/restore discipline and exposes an
 *   `onRollback` callback for telemetry hooks.
 */

import { useRef } from 'react';

import {
  useMutation,
  useQueryClient,
  type QueryKey,
  type UseMutationOptions,
  type UseMutationResult,
} from '@tanstack/react-query';

/**
 * For error surfacing in consumers, read `mutation.error` from the returned
 * UseMutationResult — it is React Query's reactive state and re-renders the
 * consumer on each error transition. An earlier revision of this hook exposed
 * a ref-backed `rollbackReason` field, but useRef writes don't trigger
 * re-renders, so the field was either stale or coincidentally correct via
 * React Query's own re-render. Removed in #1171 review followup.
 */

export interface UseOptimisticMutationOptions<TData, TError, TVariables, TCached> extends Omit<
  UseMutationOptions<TData, TError, TVariables>,
  'onMutate' | 'onError' | 'onSettled'
> {
  /** React Query cache entry whose value we mutate optimistically. */
  cacheKey: QueryKey;
  /** Pure reducer producing the optimistic next-state from the current cache value. */
  applyOptimistic: (cachedValue: TCached | undefined, variables: TVariables) => TCached;
  /**
   * Optional consumer hook fired AFTER cache restoration on rollback. Receives the
   * original variables and the network error so consumers can drive toasts or telemetry.
   * The mutation result will still surface the error via `mutation.error`.
   */
  onRollback?: (variables: TVariables, error: TError) => void;
  /**
   * If true, do not invalidate the cache on success. Default: invalidate (`refetch=true`).
   * Set to false for flows where the server response payload is already authoritative
   * and a refetch would just cost a roundtrip.
   */
  invalidateOnSuccess?: boolean;
}

export function useOptimisticMutation<TData, TError, TVariables, TCached>(
  options: UseOptimisticMutationOptions<TData, TError, TVariables, TCached>
): UseMutationResult<TData, TError, TVariables> {
  const {
    cacheKey,
    applyOptimistic,
    onRollback,
    invalidateOnSuccess = true,
    mutationFn,
    ...rest
  } = options;

  const queryClient = useQueryClient();
  const snapshotRef = useRef<TCached | undefined>(undefined);
  // Internal flag for onSettled to skip cache invalidation on the error path.
  // Not exported — consumers should read the reactive `mutation.error` instead.
  const errorOccurredRef = useRef<boolean>(false);

  const mutation = useMutation<TData, TError, TVariables>({
    ...rest,
    mutationFn,
    onMutate: async variables => {
      // Cancel in-flight queries so optimistic write isn't clobbered by stale response.
      await queryClient.cancelQueries({ queryKey: cacheKey });

      const previous = queryClient.getQueryData<TCached>(cacheKey);
      snapshotRef.current = previous;
      errorOccurredRef.current = false;

      const next = applyOptimistic(previous, variables);
      queryClient.setQueryData(cacheKey, next);

      return { previous };
    },
    onError: (error, variables) => {
      // Restore snapshot. We deliberately do not call queryClient.invalidateQueries
      // here — invalidation on error would race with the snapshot restore and
      // potentially overwrite the user's edit-in-progress in adjacent components.
      queryClient.setQueryData(cacheKey, snapshotRef.current);
      errorOccurredRef.current = true;
      onRollback?.(variables, error);
    },
    onSettled: () => {
      if (invalidateOnSuccess && !errorOccurredRef.current) {
        void queryClient.invalidateQueries({ queryKey: cacheKey });
      }
    },
  });

  return mutation;
}
