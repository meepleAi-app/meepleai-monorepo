'use client';

/**
 * Library Page Client Content
 *
 * Renders the canonical Library Hub with its carousel landing layout and the
 * AddGameDrawer (driven by `?action=add`). Legacy tab routes (`?tab=personal`,
 * `?tab=catalogo`, `?tab=wishlist`) were removed in favour of the Hub as the
 * single entry point. Wishlist remains reachable as a standalone route at
 * `/library/wishlist`.
 */

import { useEffect } from 'react';

import { FloatingActionPill } from '@/components/layout/FloatingActionPill';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useRecentsStore } from '@/stores/use-recents';

import { AddGameDrawerController } from './AddGameDrawer';
import { LibraryHub } from './LibraryHub';

// ── Loading skeleton ──────────────────────────────────────────────────────────

export function LibraryLoadingSkeleton() {
  return (
    <div className="space-y-2 sm:space-y-6">
      <Skeleton className="hidden sm:block h-24 w-full" />
      <Skeleton className="h-12 sm:h-16 w-full" />
      <div className="grid grid-cols-2 md:grid-cols-2 lg:grid-cols-3 gap-2 sm:gap-4">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-48 sm:h-64 w-full rounded-lg" />
        ))}
      </div>
    </div>
  );
}

export function LibraryContent() {
  useEffect(() => {
    useRecentsStore.getState().push({
      id: 'section-library',
      entity: 'game',
      title: 'Library',
      href: '/library',
    });
  }, []);

  return (
    <>
      <LibraryHub />
      {/* AddGameDrawer — driven by ?action=add URL param (Issue #5168) */}
      <AddGameDrawerController />
      <FloatingActionPill page="library" />
    </>
  );
}
