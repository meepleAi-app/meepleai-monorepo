'use client';

/**
 * Library Page Client Content
 * Issue #2464, #2613, #2618 — Library management
 * Issue #5042 — Tab-based routing
 * Issue #5167 — Tab rename: Games (personal) / Collection (shared catalog)
 *
 * Tab routing:
 *   (default)           → Games tab   → GamesPageClient    (personal private games)
 *   ?tab=collection     → Collection  → CollectionPageClient (shared catalog games)
 *   ?tab=wishlist       → Wishlist    → WishlistPageClient
 *
 * Note: Uses dynamicImport (renamed from 'dynamic') to avoid Turbopack
 * naming collision with the server→client boundary stub identifier.
 * See: apps/web/src/app/(chat)/chat/new/page.tsx for same pattern.
 */

import dynamicImport from 'next/dynamic';
import { useSearchParams } from 'next/navigation';

import { Skeleton } from '@/components/ui/feedback/skeleton';

// ── Loading skeleton ──────────────────────────────────────────────────────────

export function LibraryLoadingSkeleton() {
  return (
    <div className="container mx-auto px-4 py-8 space-y-6">
      <Skeleton className="h-24 w-full" />
      <Skeleton className="h-16 w-full" />
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-64 w-full rounded-lg" />
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

// ── Tab switcher (reads ?tab= search param) ───────────────────────────────────

export function LibraryContent() {
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab');

  if (tab === 'collection') return <CollectionPageClient />;
  if (tab === 'wishlist') return <WishlistPageClient />;
  return <GamesPageClient />;
}
