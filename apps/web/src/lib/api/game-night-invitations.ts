/**
 * Game-night public invitation — API client (Wave A.5b, Issue #611).
 *
 * Token-based RSVP surface delivered by Wave A.5a backend (PR #610):
 *   - GET  /api/v1/game-nights/invitations/{token}         (public, no auth)
 *   - POST /api/v1/game-nights/invitations/{token}/respond (optional auth)
 *
 * Idempotency contract D2(b) (per spec §3 of A.5a):
 *   - Same-state respond     → 200 (no-op)
 *   - Switch (Accept⇄Decline) → 409 Conflict
 *   - Terminal/past-expiry   → 410 Gone
 *
 * Why direct `fetch` instead of `apiClient.post()` (mirrors `waitlist.ts`):
 * The shared `HttpClient.createApiError()` consumes the response body once to
 * compose the user-facing message and then throws — discarding the
 * `alreadyRespondedAs` field we need from 409 to drive the FSM. The route is
 * also unauthenticated (or optional-auth) and idempotent against the token
 * unique key, so the shared retry/circuit-breaker layer adds no value.
 */

import { z } from 'zod';

import { getApiBase } from './core/httpClient';

// ========== Schemas ==========

/**
 * Mirrors backend `PublicGameNightInvitationDto` 1:1 (JSON camelCase).
 * Source: `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameNights/PublicGameNightInvitationDto.cs`
 *
 * `AlreadyRespondedAs` is `null` for pending invitations and `"Accepted"` /
 * `"Declined"` once the recipient has responded — used by the FSM to skip
 * the action affordance and surface a confirmation panel.
 */
export const PublicGameNightInvitationSchema = z.object({
  token: z.string().min(1),
  status: z.string(), // 'Pending' | 'Accepted' | 'Declined' | 'Expired' | 'Cancelled'
  expiresAt: z.string(), // ISO-8601
  respondedAt: z.string().nullable(),
  hostUserId: z.string().uuid(),
  hostDisplayName: z.string(),
  hostAvatarUrl: z.string().nullable(),
  hostWelcomeMessage: z.string().nullable(),
  gameNightId: z.string().uuid(),
  title: z.string(),
  scheduledAt: z.string(), // ISO-8601
  location: z.string().nullable(),
  durationMinutes: z.number().int().nullable(),
  expectedPlayers: z.number().int().nonnegative(),
  acceptedSoFar: z.number().int().nonnegative(),
  primaryGameId: z.string().uuid().nullable(),
  primaryGameName: z.string().nullable(),
  primaryGameImageUrl: z.string().nullable(),
  alreadyRespondedAs: z.enum(['Accepted', 'Declined']).nullable(),
});

export type PublicGameNightInvitation = z.infer<typeof PublicGameNightInvitationSchema>;

/**
 * RSVP response action. Backend accepts `"Accepted"` | `"Declined"` strings on
 * the wire (matches `GameNightInvitationStatus` enum names from A.5a).
 */
export type RsvpAction = 'Accepted' | 'Declined';

/**
 * Discriminated union covering every D2(b) outcome so the mutation hook can
 * persist a single value into FSM state. The `kind` discriminator drives
 * UI state derivation (`accepted-success` / `declined` / `token-expired` /
 * `error`) without surfacing HTTP plumbing to components.
 */
export type RespondToInvitationResult =
  | {
      readonly kind: 'success';
      readonly action: RsvpAction;
      readonly invitation: PublicGameNightInvitation;
    }
  | {
      readonly kind: 'conflict-state-switch';
      readonly currentlyRespondedAs: RsvpAction;
      readonly attemptedAction: RsvpAction;
    }
  | {
      readonly kind: 'gone';
      readonly reason: 'expired' | 'cancelled' | 'past-expiry';
    };

// ========== Errors ==========

/**
 * Network / 5xx / parse failures. Drives the generic `error` FSM state — UI
 * shows a banner with retry CTA, preserves any optimistic UI.
 */
export class InvitationFetchError extends Error {
  public readonly status: number | null;

  constructor(status: number | null, message: string, options?: { cause?: unknown }) {
    super(message, options);
    this.name = 'InvitationFetchError';
    this.status = status;
  }
}

/**
 * Distinct from `InvitationFetchError`: 404 means the token is structurally
 * unknown (typo, never minted, hard-deleted). FSM maps this to the
 * `token-invalid` state, separate from `token-expired` (410 Gone on the
 * respond endpoint, or `Expired`/`Cancelled` status on the GET).
 */
export class InvitationNotFoundError extends Error {
  constructor(token: string) {
    super(`Invitation token not found: ${token.slice(0, 6)}…`);
    this.name = 'InvitationNotFoundError';
  }
}

// ========== Server response shapes (internal) ==========

interface ServerConflictBody {
  // Backend `ConflictException` middleware shape — the relevant payload field
  // for state-switch is `alreadyRespondedAs` echoed from the entity row.
  error?: string;
  alreadyRespondedAs?: RsvpAction;
}

interface ServerGoneBody {
  error?: string;
  // 'GONE_EXPIRED' | 'GONE_CANCELLED' | 'GONE_PAST_EXPIRY' (a.5a contract)
  reason?: string;
}

// ========== Public API ==========

/**
 * GET the public invitation by token. Used both server-side (page.tsx SSR
 * fetch) and client-side (TanStack Query refetch after mutation).
 *
 * @param init optional `RequestInit` — server passes `{ cache: 'no-store' }`
 *   to bypass Next's fetch cache (token-scoped state must be fresh).
 */
export async function getInvitation(
  token: string,
  init?: RequestInit
): Promise<PublicGameNightInvitation> {
  const base = getApiBase();
  const url = `${base}/api/v1/game-nights/invitations/${encodeURIComponent(token)}`;

  let response: Response;
  try {
    response = await fetch(url, {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/json' },
      ...init,
    });
  } catch (cause) {
    throw new InvitationFetchError(null, 'Network error fetching invitation', { cause });
  }

  if (response.status === 404) {
    throw new InvitationNotFoundError(token);
  }

  if (!response.ok) {
    throw new InvitationFetchError(
      response.status,
      `Invitation fetch failed with status ${response.status}`
    );
  }

  let body: unknown;
  try {
    body = await response.json();
  } catch (cause) {
    throw new InvitationFetchError(response.status, 'Invitation response is not JSON', { cause });
  }

  const parsed = PublicGameNightInvitationSchema.safeParse(body);
  if (!parsed.success) {
    throw new InvitationFetchError(response.status, 'Invitation payload schema mismatch', {
      cause: parsed.error,
    });
  }
  return parsed.data;
}

/**
 * POST the RSVP action. Maps every D2(b) outcome onto the discriminated
 * union — never throws on 409 or 410 because those are valid FSM transitions.
 * Throws `InvitationFetchError` on 5xx / network / parse / unknown 4xx.
 */
export async function respondToInvitation(
  token: string,
  action: RsvpAction
): Promise<RespondToInvitationResult> {
  const base = getApiBase();
  const url = `${base}/api/v1/game-nights/invitations/${encodeURIComponent(token)}/respond`;

  let response: Response;
  try {
    response = await fetch(url, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify({ action }),
    });
  } catch (cause) {
    throw new InvitationFetchError(null, 'Network error responding to invitation', { cause });
  }

  // 200 same-state idempotent OR 200 fresh-success — both paths return the
  // updated invitation, FSM treats both as `success` of the requested action.
  if (response.status === 200) {
    let body: unknown;
    try {
      body = await response.json();
    } catch (cause) {
      throw new InvitationFetchError(200, 'Respond response is not JSON', { cause });
    }
    const parsed = PublicGameNightInvitationSchema.safeParse(body);
    if (!parsed.success) {
      throw new InvitationFetchError(200, 'Respond payload schema mismatch', {
        cause: parsed.error,
      });
    }
    return { kind: 'success', action, invitation: parsed.data };
  }

  if (response.status === 409) {
    const body = (await response.json().catch(() => ({}))) as ServerConflictBody;
    // Server SHOULD echo `alreadyRespondedAs` for the switch case; if it
    // doesn't, fall back to "the other action" since we know we're 409.
    const currentlyRespondedAs: RsvpAction =
      body.alreadyRespondedAs ?? (action === 'Accepted' ? 'Declined' : 'Accepted');
    return {
      kind: 'conflict-state-switch',
      currentlyRespondedAs,
      attemptedAction: action,
    };
  }

  if (response.status === 410) {
    const body = (await response.json().catch(() => ({}))) as ServerGoneBody;
    const reason: 'expired' | 'cancelled' | 'past-expiry' =
      body.reason === 'GONE_CANCELLED'
        ? 'cancelled'
        : body.reason === 'GONE_PAST_EXPIRY'
          ? 'past-expiry'
          : 'expired';
    return { kind: 'gone', reason };
  }

  if (response.status === 404) {
    throw new InvitationNotFoundError(token);
  }

  throw new InvitationFetchError(
    response.status,
    `Respond request failed with status ${response.status}`
  );
}
