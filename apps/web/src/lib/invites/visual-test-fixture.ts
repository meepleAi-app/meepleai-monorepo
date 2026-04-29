/**
 * Visual-regression test fixture for `/invites/[token]` (Wave A.5b, Issue #611).
 *
 * **Purpose**: bootstrap workflow `visual-regression-migrated.yml` runs only
 * Next.js prod (no backend API at `:8080`). The route's SSR fetcher
 * (`getInvitation`) cannot reach the backend and throws → `page.tsx` calls
 * `notFound()` → no screenshot is rendered.
 *
 * **Contract**: when env var `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`
 * is baked into the build, navigating to `/invites/<VISUAL_TEST_FIXTURE_TOKEN>`
 * short-circuits the SSR fetch and returns a deterministic, statically-defined
 * `PublicGameNightInvitation` shape that exercises the v2 surface (hero +
 * session metadata grid + accept/decline CTA bar).
 *
 * **Production safety**: Production builds do NOT set the env var. The constant
 * `IS_VISUAL_TEST_BUILD` evaluates to `false` and the fixture short-circuit
 * is dead code, eliminated by the bundler. The fixture token string is
 * meaningless to a production deployment — it returns 404 like any unknown
 * token.
 *
 * Used by:
 *   - `apps/web/e2e/visual-migrated/sp3-accept-invite.spec.ts`
 *   - `apps/web/e2e/v2-states/invites-token.spec.ts`
 */

import type { PublicGameNightInvitation } from '@/lib/api/game-night-invitations';

/**
 * Deterministic token-shaped sentinel encoding issue #611 in the suffix for
 * human-debuggability. Mirrors the `Base62.cs` 22-char alphabet but is fixed
 * (not random) — recognized by `tryLoadVisualTestFixture` only when the
 * fixture flag is enabled at build time.
 *
 * Length 22 matches the production token length (~131-bit entropy in real
 * minted tokens; this fixture is a constant string, NOT cryptographically
 * meaningful).
 */
export const VISUAL_TEST_FIXTURE_TOKEN = 'visualTestFixt0000611A' as const;

/**
 * True only when the build was produced by the visual-regression CI workflow
 * (sets `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` before `pnpm build`).
 *
 * `NEXT_PUBLIC_*` env vars are inlined at build time → in production deploys
 * this is the literal `false`, allowing the bundler to dead-code-eliminate
 * the fixture and its short-circuit branches.
 */
export const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';

/**
 * Pending invitation — covers the `default` and `logged-in` states (the
 * difference is the session, not the data shape). Schedule is well in the
 * future to avoid expired-state flakes if baselines are regenerated weeks
 * later.
 */
const FIXTURE_PENDING: PublicGameNightInvitation = {
  token: VISUAL_TEST_FIXTURE_TOKEN,
  status: 'Pending',
  expiresAt: '2026-12-31T23:59:00.000Z',
  respondedAt: null,
  hostUserId: '00000000-0000-4000-8000-000000000601',
  hostDisplayName: 'Marco Rossi',
  hostAvatarUrl: null,
  hostWelcomeMessage:
    'Pronto per una serata di Gloomhaven? Snack offerti, tu porta la tua miniatura preferita.',
  gameNightId: '00000000-0000-4000-8000-000000000611',
  title: 'Serata Gloomhaven a casa Rossi',
  scheduledAt: '2026-06-15T19:30:00.000Z',
  location: 'Via Roma 42, Milano',
  durationMinutes: 180,
  expectedPlayers: 4,
  acceptedSoFar: 2,
  primaryGameId: '00000000-0000-4000-8000-000000000174',
  primaryGameName: 'Gloomhaven',
  primaryGameImageUrl: null,
  alreadyRespondedAs: null,
};

/**
 * Already-accepted variant — exercises the success-confirmation panel without
 * requiring a mutation round-trip. Same data + `alreadyRespondedAs: 'Accepted'`.
 */
const FIXTURE_ALREADY_ACCEPTED: PublicGameNightInvitation = {
  ...FIXTURE_PENDING,
  status: 'Accepted',
  respondedAt: '2026-04-29T10:15:00.000Z',
  acceptedSoFar: 3,
  alreadyRespondedAs: 'Accepted',
};

/**
 * Already-declined variant — exercises the declined-confirmation panel.
 */
const FIXTURE_ALREADY_DECLINED: PublicGameNightInvitation = {
  ...FIXTURE_PENDING,
  status: 'Declined',
  respondedAt: '2026-04-29T10:15:00.000Z',
  alreadyRespondedAs: 'Declined',
};

/**
 * Returns the static fixture iff the build is a visual-test build AND the
 * token matches the sentinel. Returns `null` otherwise — caller must fall
 * through to the real backend fetch.
 *
 * The `variant` query parameter (`?variant=accepted|declined`) lets a single
 * page-client render multiple confirmation states from one route — visual-test
 * specs append the variant to drive the UI without a real RSVP mutation.
 */
export function tryLoadVisualTestFixture(
  token: string,
  variant?: 'pending' | 'accepted' | 'declined'
): PublicGameNightInvitation | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (token !== VISUAL_TEST_FIXTURE_TOKEN) return null;
  if (variant === 'accepted') return FIXTURE_ALREADY_ACCEPTED;
  if (variant === 'declined') return FIXTURE_ALREADY_DECLINED;
  return FIXTURE_PENDING;
}
