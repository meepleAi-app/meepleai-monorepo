/**
 * /invites/[token] — public RSVP page (V2, Wave A.5b, Issue #611).
 *
 * Server component shell:
 *   - `dynamic = 'force-dynamic'` — invite lifecycle is short (≤14d), no ISR.
 *     Each request re-fetches the public DTO so `alreadyRespondedAs` and
 *     `acceptedSoFar` are always fresh; the backend handler is cheap (single
 *     repo lookup by unique token index).
 *   - `generateMetadata({ params })` returns a STATIC title + description and
 *     `robots: { index: false, follow: false }` — the token is in the path
 *     and must NEVER be indexed by search engines or shared via OpenGraph.
 *   - SSR seed via `getInvitation(token, { cache: 'no-store' })`. On
 *     `InvitationNotFoundError` (404) the page-client renders the
 *     `token-invalid` surface (NOT `notFound()` — we want the v2 banner UI,
 *     not the global 404 page, since the user followed an inbox link they
 *     trust).
 *   - Visual-regression bootstrap escape hatch via `tryLoadVisualTestFixture`
 *     (active only when `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` is baked
 *     into the build). In production deploys the constant evaluates to
 *     `false` and the branch is dead-code-eliminated by the bundler.
 *
 * Client orchestration delegated to `./page-client.tsx`.
 *
 * Mockup parity: `admin-mockups/design_files/sp3-accept-invite.jsx`
 * Spec: `docs/superpowers/specs/2026-04-29-v2-migration-wave-a-5b-invites-token.md`
 */

import type { JSX } from 'react';

import {
  getInvitation,
  InvitationFetchError,
  InvitationNotFoundError,
  type PublicGameNightInvitation,
} from '@/lib/api/game-night-invitations';
import { tryLoadVisualTestFixture } from '@/lib/invites/visual-test-fixture';

import { InvitesTokenPageClient } from './page-client';

import type { Metadata } from 'next';

export const dynamic = 'force-dynamic';

interface InvitesTokenRouteParams {
  readonly token: string;
}

interface InvitesTokenPageProps {
  readonly params: Promise<InvitesTokenRouteParams>;
}

interface SsrInitialState {
  readonly initialData: PublicGameNightInvitation | null;
  /**
   * Forces the page-client into a banner state when SSR fetch failed in a
   * structured way (404 token unknown). For 5xx / network errors we still
   * surface the route — TanStack Query on the client will retry once and
   * fall back to the generic error banner if it cannot recover.
   */
  readonly initialBannerState: 'token-invalid' | null;
}

async function loadInitialData(token: string): Promise<SsrInitialState> {
  // Visual-regression bootstrap escape hatch (Wave A.5b, Issue #611).
  // Active only when the build was produced with
  // `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1`. Dead-code-eliminated in
  // production deploys.
  const fixture = tryLoadVisualTestFixture(token);
  if (fixture) {
    return { initialData: fixture, initialBannerState: null };
  }

  try {
    const invitation = await getInvitation(token, { cache: 'no-store' });
    return { initialData: invitation, initialBannerState: null };
  } catch (error) {
    if (error instanceof InvitationNotFoundError) {
      return { initialData: null, initialBannerState: 'token-invalid' };
    }
    if (error instanceof InvitationFetchError) {
      // Transient 5xx / network — let the client retry. Render the empty
      // shell with no initialData; useGameNightInvitation will fetch +
      // surface the generic error banner if it also fails client-side.
      return { initialData: null, initialBannerState: null };
    }
    // Unexpected — surface as token-invalid to avoid a hard crash on a
    // user-facing public route. (Schema mismatch from a backend change
    // should also be visible as token-invalid to users while we ship a fix.)
    return { initialData: null, initialBannerState: 'token-invalid' };
  }
}

export async function generateMetadata({ params: _ }: InvitesTokenPageProps): Promise<Metadata> {
  // The token MUST NOT leak to search engines or OpenGraph. We deliberately
  // do NOT fetch the invitation here — the host display name and game title
  // are private to the recipient and should not be embedded into shareable
  // metadata even if a malicious actor crawls the link.
  return {
    title: 'Invito serata gioco — MeepleAI',
    description:
      'Hai ricevuto un invito per una serata di gioco. Conferma o declina la tua presenza.',
    robots: { index: false, follow: false },
    openGraph: { title: 'Invito serata gioco — MeepleAI' },
  };
}

export default async function InvitesTokenPage({
  params,
}: InvitesTokenPageProps): Promise<JSX.Element> {
  const { token } = await params;
  const { initialData, initialBannerState } = await loadInitialData(token);

  return (
    <InvitesTokenPageClient
      token={token}
      initialData={initialData ?? undefined}
      initialBannerState={initialBannerState ?? undefined}
    />
  );
}
