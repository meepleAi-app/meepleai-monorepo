/**
 * /shared-games — public catalog index (Issue #596).
 *
 * Server component shell — exports SEO metadata + ISR `revalidate = 60`
 * (aligned with backend HybridCache TTL, see Wave A.3a spec §3.6) and seeds
 * the client body via 3-way `Promise.allSettled` SSR fetch:
 *   - searchSharedGames({ pageSize: 100 }) → initial paged catalog
 *   - getTopContributors(5)                → sidebar leaderboard
 *   - getCategories()                      → name→Guid map for genre filter
 *
 * `Promise.allSettled` keeps the page resilient to partial backend failures —
 * one rejected dependency yields graceful empty state on that surface only,
 * never a 500. Defense-in-depth try/catch logs anything that escapes (in
 * practice unreachable since `allSettled` doesn't throw).
 *
 * Mockup parity: `admin-mockups/design_files/sp3-shared-games.jsx`
 * Spec: `docs/superpowers/specs/2026-04-28-v2-migration-wave-a-3b-shared-games-fe.md` §3.5
 */

import { Suspense, type JSX } from 'react';

import {
  getCategories,
  getTopContributors,
  searchSharedGames,
  type GameCategory,
  type PagedSharedGames,
  type TopContributor,
} from '@/lib/api/shared-games';

import { SharedGamesPageClient } from './page-client';

import type { Metadata } from 'next';

export const revalidate = 60;

export const metadata: Metadata = {
  title: 'Catalogo community — MeepleAI',
  description:
    'Esplora i giochi condivisi dalla community: toolkit, agenti AI e knowledge base creati per ogni titolo. Filtra per genere, scopri i top contributors e trova ispirazione.',
  robots: { index: true, follow: true },
  openGraph: {
    title: 'Catalogo community — MeepleAI',
    description: 'Esplora i giochi condivisi dalla community: toolkit, agenti AI e knowledge base.',
    type: 'website',
  },
};

interface SsrInitialData {
  readonly initial: PagedSharedGames | null;
  readonly contributors: readonly TopContributor[];
  readonly categories: readonly GameCategory[];
}

async function loadInitialData(): Promise<SsrInitialData> {
  let initial: PagedSharedGames | null = null;
  let contributors: readonly TopContributor[] = [];
  let categories: readonly GameCategory[] = [];

  try {
    const [gamesResult, contributorsResult, categoriesResult] = await Promise.allSettled([
      searchSharedGames({ pageSize: 100 }, { next: { revalidate: 60 } }),
      getTopContributors(5, { next: { revalidate: 60 } }),
      getCategories({ next: { revalidate: 3600 } }),
    ]);

    if (gamesResult.status === 'fulfilled') {
      initial = gamesResult.value;
    }
    if (contributorsResult.status === 'fulfilled') {
      contributors = contributorsResult.value;
    }
    if (categoriesResult.status === 'fulfilled') {
      categories = categoriesResult.value;
    }
  } catch {
    // Defense in depth — Promise.allSettled doesn't reject. Fall through with defaults.
  }

  return { initial, contributors, categories };
}

export default async function SharedGamesPage(): Promise<JSX.Element> {
  const { initial, contributors, categories } = await loadInitialData();

  return (
    <Suspense fallback={<SharedGamesPageFallback />}>
      <SharedGamesPageClient
        initial={initial}
        contributors={contributors}
        categories={categories}
      />
    </Suspense>
  );
}

function SharedGamesPageFallback(): JSX.Element {
  return (
    <main aria-busy="true" aria-live="polite" className="min-h-screen bg-background">
      <div className="mx-auto max-w-[1280px] px-4 py-12 sm:px-8" />
    </main>
  );
}
