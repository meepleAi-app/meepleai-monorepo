/**
 * RSVP state machine — semantic idempotency rules (Issue #951 AC-H2, revision 2026-05-15).
 *
 * Mirrors the backend contract in
 * `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightInvitation.cs`
 * (D2 b — see spec §2.2):
 *
 * > Repeating the current response is a no-op (returns false); attempting to switch between
 * > Accepted ⇄ Declined is rejected and surfaces as 409 Conflict at the endpoint layer;
 * > responses on terminal Expired/Cancelled invitations are rejected and surface as 410 Gone.
 *
 * Why pure functions: the client mirrors the BE rules locally to short-circuit obviously
 * invalid transitions BEFORE network roundtrip (saves 8s timeout on degraded connections)
 * and to drive deterministic E2E scenarios for AC-H1 GWT failure modes.
 */

import type { RsvpStatus } from '@/lib/api/schemas/game-nights.schemas';

/** Lifecycle states an invitation can be in (mirrors BE `GameNightInvitationStatus`). */
export type InvitationLifecycle = 'Pending' | 'Accepted' | 'Declined' | 'Expired' | 'Cancelled';

/**
 * Terminal lifecycle states. Any RSVP transition attempt against these surfaces as 410 Gone.
 * Kept inline (not a Set) for tree-shaking friendliness — list is short and stable.
 */
const TERMINAL_STATES: readonly InvitationLifecycle[] = ['Expired', 'Cancelled'];

/** Response inputs the user can submit. Subset of `RsvpStatus` excluding Pending. */
export type RsvpResponse = Exclude<RsvpStatus, 'Pending'>;

export type RsvpTransitionOutcome =
  | { kind: 'no-op'; reason: 'same-response' }
  | { kind: 'allowed'; from: InvitationLifecycle; to: RsvpResponse }
  | { kind: 'rejected'; status: 409 | 410; reason: string };

export interface EvaluateRsvpInput {
  current: InvitationLifecycle;
  desired: RsvpResponse;
}

/**
 * Classify a desired RSVP transition without performing it. The hook layer uses
 * the outcome to either:
 *   - skip the network call (`no-op`)
 *   - send POST and apply optimistic update (`allowed`)
 *   - surface a toast immediately (`rejected`) without round-trip
 */
export function evaluateRsvpTransition(input: EvaluateRsvpInput): RsvpTransitionOutcome {
  const { current, desired } = input;

  if (TERMINAL_STATES.includes(current)) {
    return {
      kind: 'rejected',
      status: 410,
      reason: `Invitation is ${current.toLowerCase()}; RSVP no longer accepted.`,
    };
  }

  if (current === desired) {
    return { kind: 'no-op', reason: 'same-response' };
  }

  // Accepted ⇄ Declined is a 409 per BE D2 b. Maybe is a soft hop and is allowed
  // in both directions (BE accepts Maybe ⇄ Accepted/Declined without conflict).
  if (
    (current === 'Accepted' && desired === 'Declined') ||
    (current === 'Declined' && desired === 'Accepted')
  ) {
    return {
      kind: 'rejected',
      status: 409,
      reason: 'Cannot switch directly between Accepted and Declined.',
    };
  }

  return { kind: 'allowed', from: current, to: desired };
}
