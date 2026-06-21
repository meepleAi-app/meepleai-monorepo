/**
 * Public Game Night RSVP (anonymous /join/event/[code]) page-mock fixtures
 * (DS-17 Phase D-1 — argTypes matrix pattern).
 *
 * Consumed by `sp7-game-night-join-public` cluster Storybook story con axis matrix:
 *   state: 'pending-empty' | 'pending-filled' | 'submitting' | 'already-responded'
 *        | 'token-expired' | 'rate-limited' | 'token-invalid' | 'loading'
 *
 * Stage axis discovery: 6 STATES in sp7-game-night-join-public.jsx
 * (state-01-pending-empty through state-06-rate-limited, lines 74-82),
 * extended with `token-invalid` and `loading` for FSM completeness
 * (the FSM in `PublicJoinEventView` covers loading/invalid in addition).
 *
 * Route: `/join/event/[code]` (shipped Issue #1169). The variant is the
 * `*-public` pendant of `sp7-game-night-detail-rsvp.jsx` (host/invitee
 * authenticated). NO admin controls, NO Maybe button, NO ConnectionBar,
 * NO real-time SSE.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-1).
 */

import { http, HttpResponse, delay } from 'msw';

export type Sp7JoinPublicState =
  | 'pending-empty'
  | 'pending-filled'
  | 'submitting'
  | 'already-responded'
  | 'token-expired'
  | 'rate-limited'
  | 'token-invalid'
  | 'loading';

const TOKEN = 'tok-public-1169';

// ── Backend DTO shape mirror (PublicGameNightInvitationDto) ──────────────
const INVITATION_BASE = {
  token: TOKEN,
  status: 'Pending',
  expiresAt: '2026-05-30T20:00:00.000Z',
  respondedAt: null,
  hostUserId: '00000000-0000-4000-8000-000000000001',
  hostDisplayName: 'Marco R.',
  hostAvatarUrl: null,
  hostWelcomeMessage: 'Ci vediamo sabato per una serata di Wingspan e snack!',
  gameNightId: '00000000-0000-4000-8000-000000000002',
  title: 'Sabato sera con i Padovani',
  scheduledAt: '2026-05-23T20:00:00.000Z',
  location: 'Casa Marco · Padova',
  durationMinutes: 240,
  expectedPlayers: 6,
  acceptedSoFar: 3,
  alreadyRespondedAs: null,
  respondedByName: null,
} as const;

// ── MSW handlers ─────────────────────────────────────────────────────────

const INVITATION_URL = `*/api/v1/game-nights/invitations/${TOKEN}`;
const RESPOND_URL = `*/api/v1/game-nights/invitations/${TOKEN}/respond`;

function jsonInvitation(overrides: Record<string, unknown> = {}) {
  return HttpResponse.json({ ...INVITATION_BASE, ...overrides });
}

export function mswForSp7JoinPublicState(state: Sp7JoinPublicState) {
  // 401 on /auth/me — public surface, anonymous viewer.
  const authMe = http.get('*/api/v1/auth/me', () =>
    HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
  );

  if (state === 'loading') {
    return [authMe, http.get(INVITATION_URL, () => new Promise<Response>(() => {}))];
  }

  if (state === 'token-invalid') {
    return [
      authMe,
      http.get(INVITATION_URL, () => HttpResponse.json({ error: 'Not found' }, { status: 404 })),
    ];
  }

  if (state === 'token-expired') {
    return [
      authMe,
      http.get(INVITATION_URL, () =>
        HttpResponse.json({ error: 'Invitation expired' }, { status: 410 })
      ),
    ];
  }

  if (state === 'rate-limited') {
    return [
      authMe,
      http.get(INVITATION_URL, () =>
        HttpResponse.json(
          { error: 'Too many requests' },
          { status: 429, headers: { 'Retry-After': '12' } }
        )
      ),
    ];
  }

  if (state === 'already-responded') {
    return [
      authMe,
      http.get(INVITATION_URL, () =>
        jsonInvitation({
          status: 'Accepted',
          respondedAt: '2026-05-20T18:42:00.000Z',
          alreadyRespondedAs: 'Accepted',
          respondedByName: 'Marco',
          acceptedSoFar: 4,
        })
      ),
      http.post(RESPOND_URL, () => HttpResponse.json({ status: 'Accepted' })),
    ];
  }

  if (state === 'submitting') {
    // POST hangs to surface in-flight button + disabled inputs.
    return [
      authMe,
      http.get(INVITATION_URL, () => jsonInvitation()),
      http.post(RESPOND_URL, async () => {
        await delay('infinite');
        return HttpResponse.json({ status: 'Accepted' });
      }),
    ];
  }

  // pending-empty + pending-filled share the same baseline payload — the
  // "filled" affordance is purely a Story-side initial-display-name seed.
  return [
    authMe,
    http.get(INVITATION_URL, () => jsonInvitation()),
    http.post(RESPOND_URL, () => HttpResponse.json({ status: 'Accepted' })),
  ];
}

// ── Token export ─────────────────────────────────────────────────────────
// Re-exported so the Story can pass it as the `code` prop to `PublicJoinEventView`.
export const MOCK_SP7_JOIN_PUBLIC_TOKEN = TOKEN;
