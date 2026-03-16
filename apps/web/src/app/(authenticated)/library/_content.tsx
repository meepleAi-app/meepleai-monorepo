'use client';

/**
 * Library Page Client Content
 * Issue #2464, #2613, #2618 — Library management
 * Issue #5042 — Tab-based routing
 * Issue #5167 — Tab rename: Games (personal) / Collection (shared catalog)
 * Issue #5168 — AddGameDrawer (right-side Sheet for adding games)
 *
 * Tab routing:
 *   (default)           → Collection  → CollectionPageClient (shared catalog games)
 *   ?tab=private        → Games tab   → GamesPageClient    (personal private games)
 *   ?tab=wishlist       → Wishlist    → WishlistPageClient
 *
 * Action routing:
 *   ?action=add         → AddGameDrawer opens (wizard: manual or from catalog)
 *
 * Note: Uses dynamicImport (renamed from 'dynamic') to avoid Turbopack
 * naming collision with the server→client boundary stub identifier.
 * See: apps/web/src/app/(chat)/chat/new/page.tsx for same pattern.
 */

import { useEffect } from 'react';

import dynamicImport from 'next/dynamic';
import { useSearchParams } from 'next/navigation';

import { UsageWidget } from '@/components/library/UsageWidget';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useCardHand } from '@/stores/use-card-hand';

import { AddGameDrawerController } from './AddGameDrawer';

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

// ── Dynamic imports (ssr: false avoids DOMMatrix / framer-motion issues) ─────

// Default tab: personal games (Issue #5167 — was PrivateGamesClient)
const GamesPageClient = dynamicImport(() => import('./private/PrivateGamesClient'), {
  ssr: false,
  loading: () => <LibraryLoadingSkeleton />,
});

// Collection tab: shared catalog games (Issue #5167 — was LibraryPageClient)
const CollectionPageClient = dynamicImport(() => import('./CollectionPageClient'), {
  ssr: false,
  loading: () => <LibraryLoadingSkeleton />,
});

// Import WishlistPage directly — it is a clean client component (no LibraryNavTabs)
const WishlistPageClient = dynamicImport(() => import('./wishlist/page'), {
  ssr: false,
  loading: () => <LibraryLoadingSkeleton />,
});

// Proposals tab: user's game proposals to shared catalog
const ProposalsPageClient = dynamicImport(() => import('./proposals/MyProposalsClient'), {
  ssr: false,
  loading: () => <LibraryLoadingSkeleton />,
});

// ── Tab switcher + drawer controller ──────────────────────────────────────────

export function LibraryContent() {
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab');
  const { drawCard } = useCardHand();

  useEffect(() => {
    drawCard({
      id: 'section-library',
      entity: 'game',
      title: 'Library',
      href: '/library',
    });
  }, [drawCard]);

  return (
    <>
      {/* Layout: main content + sidebar */}
      <div className="flex gap-6 items-start">
        {/* Tab content — takes remaining width */}
        <div className="min-w-0 flex-1">
          {tab === 'proposals' ? (
            <ProposalsPageClient />
          ) : tab === 'private' ? (
            <GamesPageClient />
          ) : tab === 'wishlist' ? (
            <WishlistPageClient />
          ) : (
            <CollectionPageClient />
          )}
        </div>

        {/* Usage widget — sticky sidebar on md+ screens */}
        <aside className="hidden lg:block w-64 flex-shrink-0">
          <div className="sticky top-20">
            <UsageWidget />
          </div>
        </aside>
      </div>

      {/* AddGameDrawer — driven by ?action=add URL param (Issue #5168) */}
      <AddGameDrawerController />
    </>
  );
}
