/**
 * /join/event/[code] — public game-night RSVP page (issue #1169).
 *
 * PUBLIC page (no auth required, no SSR session reuse). Loads a game-night
 * invitation by opaque token (the `code` param) and renders an anonymous
 * RSVP surface — guest types an optional display name + chooses
 * Accept / Decline, backed by the rate-limited public endpoints:
 *   - GET  /api/v1/game-nights/invitations/{token}         (60 req/min/IP)
 *   - POST /api/v1/game-nights/invitations/{token}/respond (10 req/min/IP)
 *
 * Server component shell:
 *   - `dynamic = 'force-dynamic'` — token state (responded? expired?) changes
 *     fast and must always be fresh; no caching.
 *   - `generateMetadata` returns a STATIC, non-indexable title/description.
 *     The token is in the URL and MUST NOT leak via OpenGraph / search.
 *   - Suspense boundary for the thin client orchestrator. We delegate the
 *     full GET/POST + FSM lifecycle to TanStack Query on the client (no SSR
 *     seed) because the anonymous fetch is cheap, the route is rarely SSRd,
 *     and routing this through SSR adds a hard dependency on the API being
 *     reachable from the Next.js process at request time.
 *
 * Sibling route reference: `/join/session/[code]` (live session guest viewer)
 * and `/invites/[token]` (legacy RSVP surface, Wave A.5b) — both proven
 * patterns for public token-bound surfaces.
 */

import { Suspense, type JSX, use } from 'react';

import { PublicJoinEventView } from './_components/PublicJoinEventView';

import type { Metadata } from 'next';

export const dynamic = 'force-dynamic';

interface PublicJoinEventPageProps {
  readonly params: Promise<{ code: string }>;
}

export function generateMetadata(): Metadata {
  // Token is in the URL — must not be indexable. We also do NOT fetch the
  // invitation server-side because the host display name + game title are
  // private to the recipient (same posture as /invites/[token]).
  return {
    title: 'RSVP serata gioco — MeepleAI',
    description:
      'Conferma o declina la tua partecipazione alla serata di gioco a cui sei stato invitato.',
    robots: { index: false, follow: false },
    openGraph: { title: 'RSVP serata gioco — MeepleAI' },
  };
}

export default function PublicJoinEventPage({ params }: PublicJoinEventPageProps): JSX.Element {
  const { code } = use(params);

  return (
    <Suspense
      fallback={
        <main className="flex min-h-screen items-center justify-center bg-background p-4">
          <p className="text-sm text-muted-foreground">Caricamento invito…</p>
        </main>
      }
    >
      <PublicJoinEventView code={code} />
    </Suspense>
  );
}
