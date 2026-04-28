/**
 * /shared-games — public V2 catalog page (Wave A.3b, Issue #596).
 *
 * Server component shell. Exports `metadata` for SEO/OpenGraph and renders
 * the client body inside a Suspense boundary so React Query / URL-hash
 * hydration doesn't block the streaming envelope.
 *
 * Why no SSR fetch here:
 *   - The repo doesn't use `HydrationBoundary` / `dehydrate` anywhere yet,
 *     so introducing per-route SSR cache plumbing for a single page would
 *     be premature complexity. The client query has `staleTime: 5min` and
 *     `placeholderData: keepPreviousData`, which keeps the UX smooth even
 *     without server-prefetched data.
 *   - The `useSharedGamesSearch` hook already accepts an `initialData`
 *     option (honoured only when state matches defaults), so a future PR
 *     can wire SSR prefetch without changing the client surface.
 *
 * Spec: docs/superpowers/specs/2026-04-28-v2-migration-wave-a-3b-shared-games-fe.md
 */

import { Suspense, type JSX } from 'react';

import { SharedGamesPageClient } from './page-client';

import type { Metadata } from 'next';

// NOTE: Next.js `metadata` runs in a server context where `useTranslation()` is
// not available. Since the project's default locale is IT (see locales/index.ts)
// we hardcode the IT copy here, mirroring `pages.sharedGames.metaTitle` /
// `pages.sharedGames.metaDescription` in `locales/it.json`. A future PR with
// per-locale routing can replace this with `generateMetadata({ params })`.
export const metadata: Metadata = {
  title: 'Catalogo giochi della community — MeepleAI',
  description:
    'Scopri i giochi con toolkit AI, agenti dedicati e i contributi più recenti dei membri della community MeepleAI.',
  robots: { index: true, follow: true },
};

export default function SharedGamesPage(): JSX.Element {
  return (
    <Suspense fallback={<SharedGamesFallback />}>
      <SharedGamesPageClient />
    </Suspense>
  );
}

function SharedGamesFallback(): JSX.Element {
  return (
    <main aria-busy="true" aria-live="polite" className="min-h-screen bg-background">
      <div className="mx-auto max-w-[1280px] px-4 py-12 sm:px-8" />
    </main>
  );
}
