/**
 * User Library Page
 * Issue #2464, #2613, #2618 — Library management
 * Issue #5042 — Tab-based routing (replaces sub-page navigation)
 *
 * Handles three tabs via ?tab search param:
 *   (default)       → I miei giochi (LibraryPageClient)
 *   ?tab=wishlist   → Wishlist
 *   ?tab=private    → Giochi Privati
 *
 * MiniNav tabs are registered by layout.tsx.
 * LibraryNavTabs removed — navigation is now via MiniNav (LayoutShell).
 *
 * Note: Uses next/dynamic with ssr: false to avoid DOMMatrix SSR issues
 * with framer-motion during static page generation.
 */

'use client';

import { Suspense } from 'react';

import dynamic from 'next/dynamic';
import { useSearchParams } from 'next/navigation';

import { RequireRole } from '@/components/auth/RequireRole';
import { Skeleton } from '@/components/ui/feedback/skeleton';

import { LibraryNavConfig } from './NavConfig';

// Dynamically import the client component with SSR disabled
// This prevents framer-motion from being evaluated during SSG
const LibraryPageClient = dynamic(() => import('./LibraryPageClient'), {
  ssr: false,
  loading: () => (
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

const LibraryPageClient = dynamic(() => import('./LibraryPageClient'), {
  ssr: false,
  loading: () => <LibraryLoadingSkeleton />,
});

// Import WishlistPage directly — it is a clean client component (no LibraryNavTabs)
const WishlistPageClient = dynamic(() => import('./wishlist/page'), {
  ssr: false,
  loading: () => <LibraryLoadingSkeleton />,
});

// Import PrivateGamesClient directly — private/page.tsx wraps LibraryNavTabs (deprecated)
const PrivateGamesClient = dynamic(() => import('./private/PrivateGamesClient'), {
  ssr: false,
  loading: () => <LibraryLoadingSkeleton />,
});

// ── Tab switcher (reads ?tab= search param) ───────────────────────────────────

function LibraryContent() {
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab');

  if (tab === 'wishlist') return <WishlistPageClient />;
  if (tab === 'private') return <PrivateGamesClient />;
  return <LibraryPageClient />;
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function LibraryPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <LibraryNavConfig />
      <LibraryPageClient />
    </RequireRole>
  );
}
