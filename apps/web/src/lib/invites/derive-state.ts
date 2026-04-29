/**
 * /invites/[token] — pure FSM helpers (Wave A.5b, Issue #611).
 *
 * Extracted from `page-client.tsx` so the 7-state derivation can be unit-tested
 * without React rendering overhead. Pure, deterministic, no React/Next deps.
 *
 * 7-state surface (mirrors spec §3.2):
 *   default | logged-in | accepted-success | declined |
 *   token-expired | token-invalid | already-accepted
 *
 * Resolution priority (top wins):
 *   1. `?state=...` visual-test override (dev/CI only)
 *   2. SSR `initialBannerState === 'token-invalid'` (404 token)
 *   3. Mutation outcome (`success | gone`) — reflects user's last interaction,
 *      outranks stale GET cache
 *   4. Server-side status snapshot (`Expired`/`Cancelled` → expired,
 *      `alreadyRespondedAs` non-null → already-accepted)
 *   5. Pending invitation, anonymous vs logged-in for pre-fill UX
 *
 * The `conflict-state-switch` mutation kind intentionally does NOT transition
 * the FSM — it surfaces as a transient banner over the existing surface and
 * the page re-renders after the caller's `refetch()` brings fresh data.
 */

import type { PublicGameNightInvitation, RsvpAction } from '@/lib/api/game-night-invitations';

export type InviteState =
  | 'default'
  | 'logged-in'
  | 'accepted-success'
  | 'declined'
  | 'token-expired'
  | 'token-invalid'
  | 'already-accepted';

export const VALID_STATE_OVERRIDES: ReadonlySet<InviteState> = new Set([
  'default',
  'logged-in',
  'accepted-success',
  'declined',
  'token-expired',
  'token-invalid',
  'already-accepted',
]);

export interface DeriveStateArgs {
  readonly data: PublicGameNightInvitation | undefined;
  readonly hasSession: boolean;
  readonly mutationKind: 'success' | 'conflict-state-switch' | 'gone' | null;
  readonly mutationAction: RsvpAction | null;
  readonly initialBannerState: 'token-invalid' | undefined;
  readonly stateOverride: InviteState | undefined;
}

export function deriveState({
  data,
  hasSession,
  mutationKind,
  mutationAction,
  initialBannerState,
  stateOverride,
}: DeriveStateArgs): InviteState {
  // 1. Visual-test override (dev/CI only).
  if (stateOverride) return stateOverride;

  // 2. SSR-provided structural error.
  if (initialBannerState === 'token-invalid') return 'token-invalid';

  // 3. Mutation-driven transitions outrank stale GET data — they reflect the
  //    user's last interaction.
  if (mutationKind === 'success' && mutationAction === 'Accepted') return 'accepted-success';
  if (mutationKind === 'success' && mutationAction === 'Declined') return 'declined';
  if (mutationKind === 'gone') return 'token-expired';
  // (Conflict surfaces as a banner overlaid on the existing surface; it does
  //  not transition the FSM. Caller derives the banner separately.)

  // 4. Server-side status snapshots (SSR seed or refetch).
  if (!data) {
    // No SSR data, no token-invalid signal — render the default surface and
    // let the client query retry. The hook surfaces a generic error banner
    // if it also fails.
    return hasSession ? 'logged-in' : 'default';
  }
  if (data.status === 'Expired' || data.status === 'Cancelled') return 'token-expired';
  if (data.alreadyRespondedAs === 'Accepted' || data.alreadyRespondedAs === 'Declined') {
    return 'already-accepted';
  }

  // 5. Pending invitation. Distinguish anonymous vs logged-in for pre-fill UX.
  return hasSession ? 'logged-in' : 'default';
}

/**
 * Parse the `?state=...` URL param into a typed override, returning `undefined`
 * if the override mechanism is disabled (production build w/o visual-test flag)
 * or the value isn't one of the 7 valid states.
 */
export function parseStateOverride(
  raw: string | null,
  options: { readonly enabled: boolean }
): InviteState | undefined {
  if (!options.enabled || !raw) return undefined;
  return VALID_STATE_OVERRIDES.has(raw as InviteState) ? (raw as InviteState) : undefined;
}
