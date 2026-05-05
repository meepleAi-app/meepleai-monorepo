/**
 * useGameNightInvitation — TanStack Query hook for /invites/[token].
 *
 * Wave A.5b (Issue #611). Wraps `getInvitation()` from
 * `lib/api/game-night-invitations.ts` with a stable query key and SSR seed
 * support, mirroring `useSharedGameDetail` (Wave A.4).
 *
 * Contract:
 *  - `queryKey: ['game-night-invitation', token]` — token is the unique
 *    addressing key (no filters / sort to encode).
 *  - `initialData` from SSR seeds the cache; `staleTime: 60_000` aligns with
 *    the brief invite lifecycle and avoids immediate background refetch on
 *    mount when SSR already fetched fresh data.
 *  - `enabled: token.length > 0` defends against router transitions where
 *    the param is briefly empty.
 *  - `retry: false` for `InvitationNotFoundError` (404 is a UX state, not a
 *    transient error). Other errors retry once.
 *
 * Refetch is invoked by `useRespondToInvitation` after a 409 conflict to
 * re-derive the FSM state from a fresh `alreadyRespondedAs` value.
 */

'use client';

import { useQuery } from '@tanstack/react-query';

import {
  getInvitation,
  InvitationNotFoundError,
  type PublicGameNightInvitation,
} from '@/lib/api/game-night-invitations';

const STALE_MS = 60_000;

export interface UseGameNightInvitationArgs {
  readonly token: string;
  /** SSR seed; passed through to React Query as `initialData`. */
  readonly initialData?: PublicGameNightInvitation;
}

export interface UseGameNightInvitationResult {
  readonly data: PublicGameNightInvitation | undefined;
  readonly isLoading: boolean;
  readonly isFetching: boolean;
  readonly isError: boolean;
  readonly error: Error | null;
  readonly refetch: () => Promise<void>;
}

export function useGameNightInvitation(
  args: UseGameNightInvitationArgs
): UseGameNightInvitationResult {
  const queryKey = ['game-night-invitation', args.token] as const;

  const result = useQuery<PublicGameNightInvitation, Error>({
    queryKey,
    queryFn: () => getInvitation(args.token),
    initialData: args.initialData,
    staleTime: STALE_MS,
    enabled: args.token.length > 0,
    retry: (failureCount, error) => {
      // 404 token is a structural UX state, not a transient error — don't retry.
      if (error instanceof InvitationNotFoundError) return false;
      return failureCount < 1;
    },
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
