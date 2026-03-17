import { Suspense } from 'react';

import { type Metadata } from 'next';

import { GameCatalogGrid } from '@/components/admin/shared-games/game-catalog-grid';
import { GameFilters } from '@/components/admin/shared-games/game-filters';
import { RecentlyProcessedWidget } from '@/components/admin/shared-games/RecentlyProcessedWidget';

export const metadata: Metadata = {
  title: 'All Games',
  description: 'Browse and manage the shared game catalog',
};

function CardSkeleton({ height = 'h-[600px]' }: { height?: string }) {
  return (
    <div
      className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`}
    />
  );
}

export default function AllGamesPage() {
  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          All Games
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Browse and manage the shared game catalog
        </p>
      </div>

      {/* Recently Processed PDFs */}
      <RecentlyProcessedWidget />

      {/* Filters */}
      <Suspense fallback={<CardSkeleton height="h-[160px]" />}>
        <GameFilters />
      </Suspense>

      {/* Game Grid */}
      <Suspense fallback={<CardSkeleton />}>
        <GameCatalogGrid />
      </Suspense>
    </div>
  );
}
