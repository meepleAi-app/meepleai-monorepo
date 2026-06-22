/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or admin-decorative inline gradient; DS-13a admin scope, mockup .e-bg pattern. Future: extract --admin-* token family (deferred to DS-15 audit). */
import { Suspense } from 'react';

import { type Metadata } from 'next';

import { CategoriesTable } from '@/components/admin/shared-games/categories-table';

export const metadata: Metadata = {
  title: 'Categories',
  description: 'Organize and manage game categories',
};

function CardSkeleton({ height = 'h-[600px]' }: { height?: string }) {
  return (
    <div
      className={`${height} bg-card/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-border/60 dark:border-zinc-700/40 animate-pulse`}
    />
  );
}

export default function CategoriesPage() {
  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Game Categories
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Organize and manage game categories
        </p>
      </div>

      {/* Categories Table */}
      <Suspense fallback={<CardSkeleton />}>
        <CategoriesTable />
      </Suspense>
    </div>
  );
}
