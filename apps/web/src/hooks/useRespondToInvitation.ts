/**
 * useRespondToInvitation — RSVP mutation FSM hook for /invites/[token].
 *
 * Wave A.5b (Issue #611). Wraps `respondToInvitation()` in a TanStack Query
 * mutation and exposes a 5-state FSM that the page-client renders against.
 *
 *     idle ─submit→ submitting ─{200 success}→  success      (with action accepted/declined)
 *                                ─{409}→         conflict     (state switch attempted)
 *                                ─{410}→         gone         (terminal: expired/cancelled)
 *                                ─{5xx,network}→ error
 *     conflict ─submit→ submitting (caller can re-attempt)
 *     gone / success ─reset→ idle
 *
 * The hook is intentionally form-agnostic. The page-client is responsible for:
 *   - Calling `refetch` on the GET query after a 409 (so `alreadyRespondedAs`
 *     surfaces and the FSM derives `already-accepted` / `declined`).
 *   - Mounting the confetti overlay on `success` of action `'Accepted'`.
 *
 * `respondToInvitation` never throws on 409/410 (those are valid FSM
 * transitions); it throws only on 5xx / network / parse / unknown 4xx.
 */

'use client';

import { useCallback, useMemo } from 'react';

import { useMutation } from '@tanstack/react-query';

import {
  respondToInvitation,
  type RespondToInvitationResult,
  type RsvpAction,
} from '@/lib/api/game-night-invitations';

export type RespondState = 'idle' | 'submitting' | 'success' | 'conflict' | 'gone' | 'error';

export interface UseRespondToInvitationArgs {
  readonly token: string;
}

export interface UseRespondToInvitationReturn {
  readonly state: RespondState;
  readonly result: RespondToInvitationResult | null;
  readonly error: Error | null;
  readonly submit: (action: RsvpAction) => Promise<RespondToInvitationResult | null>;
  readonly reset: () => void;
}

interface MutationVariables {
  readonly token: string;
  readonly action: RsvpAction;
}

function deriveState(
  status: 'idle' | 'pending' | 'success' | 'error',
  result: RespondToInvitationResult | null
): RespondState {
  if (status === 'pending') return 'submitting';
  if (status === 'error') return 'error';
  if (status === 'success' && result) {
    if (result.kind === 'success') return 'success';
    if (result.kind === 'conflict-state-switch') return 'conflict';
    if (result.kind === 'gone') return 'gone';
  }
  return 'idle';
}

export function useRespondToInvitation(
  args: UseRespondToInvitationArgs
): UseRespondToInvitationReturn {
  const mutation = useMutation<RespondToInvitationResult, Error, MutationVariables>({
    mutationFn: ({ token, action }) => respondToInvitation(token, action),
  });

  const state = useMemo(
    () => deriveState(mutation.status, mutation.data ?? null),
    [mutation.status, mutation.data]
  );

  const submit = useCallback(
    async (action: RsvpAction): Promise<RespondToInvitationResult | null> => {
      try {
        const res = await mutation.mutateAsync({ token: args.token, action });
        return res;
      } catch {
        // Error state is exposed via `error` field; swallow the throw so the
        // form can re-enable without an unhandled rejection bubbling up.
        return null;
      }
    },
    [mutation, args.token]
  );

  const reset = useCallback(() => {
    mutation.reset();
  }, [mutation]);

  return {
    state,
    result: mutation.data ?? null,
    error: mutation.error ?? null,
    submit,
    reset,
  };
}
