/**
 * Discover Page — Community Game Catalog
 * Issue #5041 — Migrate Game Catalog into /discover
 *
 * Tabs:
 * - catalog (default): Full shared games catalog via CatalogContent
 * - proposals: User game proposals via MyProposalsClient
 * - community: Placeholder for future community features
 * - bgg: Direct BGG search via BggSearchTab
 */

import { Suspense } from 'react';

import MyProposalsClient from '@/app/(authenticated)/library/proposals/MyProposalsClient';
import { CatalogContent } from '@/app/(public)/games/catalog/_content';
import { MeepleGameCatalogCardSkeleton } from '@/components/catalog/MeepleGameCatalogCard';
import { DiscoverVisitTracker } from '@/components/onboarding/DiscoverVisitTracker';

import { BggSearchTab } from './BggSearchTab';

interface DiscoverPageProps {
  searchParams: Promise<{ tab?: string }>;
}

function CatalogSkeleton() {
  return (
    <div className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
      <div className="container mx-auto px-4 py-8 max-w-7xl">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <MeepleGameCatalogCardSkeleton key={i} />
          ))}
        </div>
      </div>
    </div>
  );
}

export default async function DiscoverPage({ searchParams }: DiscoverPageProps) {
  const params = await searchParams;
  const tab = params.tab ?? 'catalog';

  // Track discover visit for onboarding checklist (all tabs count)
  const tracker = <DiscoverVisitTracker />;

  if (tab === 'proposals') {
    return (
      <>
        {tracker}
        <MyProposalsClient catalogBasePath="/discover" />
      </>
    );
  }

  if (tab === 'community') {
    return (
      <>
        {tracker}
        <div className="min-h-screen bg-background">
          <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
            <h1 className="text-2xl font-bold tracking-tight text-foreground">Community</h1>
            <p className="mt-2 text-muted-foreground">Funzionalità community in arrivo.</p>
          </div>
        </div>
      </>
    );
  }

  if (tab === 'bgg') {
    return (
      <>
        {tracker}
        <BggSearchTab />
      </>
    );
  }

  return (
    <>
      {tracker}
      <Suspense fallback={<CatalogSkeleton />}>
        <CatalogContent gameDetailBasePath="/discover" />
      </Suspense>
    </>
  );
}
