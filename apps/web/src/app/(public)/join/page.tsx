/**
 * /join — public Alpha-program waitlist enrollment (V2, Wave A.2, Issue #589).
 *
 * Server component shell — exports `metadata` for SEO/social cards (IT) and
 * renders the client body inside a Suspense boundary. We avoid a nested
 * `layout.tsx` here because the sibling route `(public)/join/session/[code]`
 * (guest game-night view) would otherwise inherit our title/description.
 *
 * Mockup parity: `admin-mockups/design_files/sp3-join.jsx`
 * Spec: `docs/superpowers/specs/2026-04-27-v2-migration-wave-a-2-join.md`
 */

import { Suspense, type JSX } from 'react';

import { JoinPageClient } from './page-client';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: "Entra nell'Alpha — MeepleAI",
  description:
    'Iscriviti alla waitlist privata di MeepleAI: agenti AI per i tuoi giochi da tavolo, library smart e session live.',
  robots: { index: true, follow: true },
};

/**
 * Suspense fallback mirrors the shell that the client renders post-hydration
 * so the layout doesn't shift between SSR and CSR. The client component bails
 * out of CSR via React Query (`useMutation`), so the boundary is mostly
 * symbolic — but it gives Next.js a stable streaming envelope.
 */
export default function JoinPage(): JSX.Element {
  return (
    <Suspense fallback={<JoinPageFallback />}>
      <JoinPageClient />
    </Suspense>
  );
}

function JoinPageFallback(): JSX.Element {
  return (
    <main aria-busy="true" aria-live="polite" className="min-h-screen bg-background">
      <div className="mx-auto max-w-[1280px] px-4 py-12 sm:px-8" />
    </main>
  );
}
