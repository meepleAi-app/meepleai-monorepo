/**
 * useGameNightDetail — composed hook for `/game-nights/[id]` (Issue #951, commit 3).
 *
 * Wires together:
 *   - useGameNight(id)        → event aggregate
 *   - useGameNightRsvps(id)   → roster
 *   - classifyGameNightActor  → host/guest/bystander discrimination (AC-H3)
 *   - evaluateRsvpTransition  → client-side pre-validation of RSVP submits (AC-H2)
 *   - useOptimisticMutation   → < 100ms perceived RSVP latency w/ rollback (AC-H1)
 *
 * Why a composed hook rather than wiring inline in the page-client:
 *   - The classification + state-machine + optimistic mutation chain has its own
 *     invariants that are easier to test in isolation. The page-client stays a
 *     thin renderer over the returned shape.
 *   - Future call sites (mobile bottom-sheet variant, host-side roster mgmt)
 *     will reuse the same composition.
 *
 * Returned `submitRsvp` semantics:
 *   - Calls `evaluateRsvpTransition` first; for `no-op` returns silently.
 *   - For `rejected` returns a `'rejected'` outcome containing the reason key
 *     so the caller can show a toast (no network call).
 *   - For `allowed` triggers the optimistic mutation and returns `'submitted'`.
 *
 * The hook stays i18n-agnostic: error keys / status are surfaced as enums; the
 * page-client maps to localized strings via `useTranslation`.
 */

import { useMemo } from 'react';

import { useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  GameNightDto,
  GameNightRsvpDto,
  RsvpStatus,
} from '@/lib/api/schemas/game-nights.schemas';
import {
  classifyGameNightActor,
  type GameNightActorContext,
} from '@/lib/game-nights/actor-classification';
import {
  evaluateRsvpTransition,
  type InvitationLifecycle,
  type RsvpResponse,
} from '@/lib/game-nights/rsvp-state-machine';
import { useOptimisticMutation } from '@/lib/hooks/use-optimistic-mutation';

import { gameNightKeys, useGameNight, useGameNightRsvps } from './useGameNights';

/** Mirror of `RsvpStatus` minus Pending; matches state-machine `RsvpResponse`. */
function toRsvpResponse(status: RsvpStatus): RsvpResponse | undefined {
  return status === 'Pending' ? undefined : status;
}

/**
 * Project a viewer's RsvpStatus into an `InvitationLifecycle` understood by the
 * state machine. Maybe is mapped to Pending — the state machine treats it as a
 * non-terminal, non-conflicting state, which matches BE behaviour (Maybe is a
 * "soft hop" between Accepted/Declined).
 *
 * When the event itself is Cancelled we short-circuit to the terminal
 * 'Cancelled' lifecycle so any RSVP attempt surfaces as 410 Gone client-side.
 */
function deriveLifecycle(
  viewerRsvp: GameNightRsvpDto | undefined,
  event: GameNightDto
): InvitationLifecycle {
  if (event.status === 'Cancelled') return 'Cancelled';
  if (event.status === 'Completed') return 'Expired';
  if (!viewerRsvp) return 'Pending';
  if (viewerRsvp.status === 'Maybe') return 'Pending';
  return viewerRsvp.status;
}

export type RsvpSubmitOutcome =
  | { kind: 'no-op' }
  | { kind: 'submitted'; response: RsvpResponse }
  | { kind: 'rejected'; status: 409 | 410; reason: string };

export interface UseGameNightDetailReturn {
  readonly event: GameNightDto | undefined;
  readonly rsvps: readonly GameNightRsvpDto[] | undefined;
  readonly actor: GameNightActorContext | undefined;
  readonly isLoading: boolean;
  readonly isError: boolean;
  readonly error: Error | null;
  /** Current server-truth viewer response (undefined when bystander/host or still Pending). */
  readonly currentResponse: RsvpResponse | undefined;
  /** In-flight optimistic response, or null when no mutation pending. */
  readonly pendingResponse: RsvpResponse | null;
  /**
   * Submit a new RSVP response. Pure orchestration — emits an outcome
   * descriptor so the caller can drive toasts. Network call is skipped when
   * outcome is 'no-op' or 'rejected'.
   */
  readonly submitRsvp: (response: RsvpResponse) => RsvpSubmitOutcome;
  /** Indicates whether the optimistic mutation is currently running. */
  readonly isSubmitting: boolean;
  /** Last rollback error (populated only on the most recent failed submit). */
  readonly rollbackError: Error | undefined;
}

interface RsvpMutationVariables {
  readonly response: RsvpResponse;
}

export function useGameNightDetail(
  id: string,
  viewerId: string | undefined
): UseGameNightDetailReturn {
  const queryClient = useQueryClient();

  const eventQuery = useGameNight(id);
  const rsvpsQuery = useGameNightRsvps(id);

  const event = eventQuery.data;
  const rsvps = rsvpsQuery.data;

  const actor = useMemo<GameNightActorContext | undefined>(() => {
    if (!viewerId || !event) return undefined;
    return classifyGameNightActor({ viewerId, event, rsvps });
  }, [viewerId, event, rsvps]);

  const viewerRsvp = actor?.actor === 'guest' ? actor.rsvp : undefined;
  const currentResponse = viewerRsvp ? toRsvpResponse(viewerRsvp.status) : undefined;

  const cacheKey = gameNightKeys.rsvps(id);

  const mutation = useOptimisticMutation<
    void,
    Error,
    RsvpMutationVariables,
    readonly GameNightRsvpDto[]
  >({
    cacheKey,
    mutationFn: ({ response }) => api.gameNights.rsvp(id, response),
    applyOptimistic: (cached, { response }) => {
      const current = cached ?? [];
      if (!viewerId) return current;
      const idx = current.findIndex(r => r.userId === viewerId);
      if (idx < 0) return current;
      const next = current.slice();
      const previousEntry = next[idx];
      if (!previousEntry) return current;
      next[idx] = {
        ...previousEntry,
        status: response,
        respondedAt: new Date().toISOString(),
      };
      return next;
    },
    onSuccess: () => {
      // The event aggregate carries acceptedCount/pendingCount that depend on
      // the roster, so we explicitly invalidate the detail too. The roster
      // cache is invalidated automatically by useOptimisticMutation.
      void queryClient.invalidateQueries({ queryKey: gameNightKeys.detail(id) });
      void queryClient.invalidateQueries({ queryKey: gameNightKeys.all });
    },
  });

  const pendingResponse: RsvpResponse | null =
    mutation.isPending && mutation.variables ? mutation.variables.response : null;

  function submitRsvp(response: RsvpResponse): RsvpSubmitOutcome {
    if (!event) {
      return {
        kind: 'rejected',
        status: 410,
        reason: 'Event not loaded.',
      };
    }
    const lifecycle = deriveLifecycle(viewerRsvp, event);
    const outcome = evaluateRsvpTransition({ current: lifecycle, desired: response });
    if (outcome.kind === 'no-op') return { kind: 'no-op' };
    if (outcome.kind === 'rejected') {
      return { kind: 'rejected', status: outcome.status, reason: outcome.reason };
    }
    mutation.mutate({ response });
    return { kind: 'submitted', response };
  }

  return {
    event,
    rsvps,
    actor,
    isLoading: eventQuery.isLoading || rsvpsQuery.isLoading,
    isError: eventQuery.isError || rsvpsQuery.isError,
    error: eventQuery.error ?? rsvpsQuery.error ?? null,
    currentResponse,
    pendingResponse,
    submitRsvp,
    isSubmitting: mutation.isPending,
    rollbackError: mutation.rollbackReason,
  };
}
