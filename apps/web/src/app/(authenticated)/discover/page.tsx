'use client';

import { Suspense } from 'react';

import { DiscoverHub } from '@/components/features/discover/DiscoverHub';

/**
 * `/discover` — standalone Discover route. Preserved for backward compat
 * (existing bookmarks + cross-links) after Asse D follow-up P2 (#1899) moved
 * the canonical Discover surface to `/games?tab=discover`.
 *
 * The Discover content itself lives in `DiscoverHub`
 * (`components/features/discover/DiscoverHub.tsx`), which calls
 * `useSearchParams()` — the Suspense wrapper is required by App Router to
 * avoid the CSR-bailout error.
 *
 * #2158 (visual-smoke follow-up): the original MiniNavSlot config registered
 * a single tab that duplicated the breadcrumb. Convention: MiniNavSlot only
 * carries genuine multi-tab navigation (≥2 alternatives) — single-tab configs
 * are pure noise. The canonical multi-tab Discover surface lives at
 * `/games?tab=discover` (see `app/(authenticated)/games/page.tsx`), which is
 * where the MiniNavSlot strip belongs.
 */
export default function DiscoverPage() {
  return (
    <Suspense fallback={null}>
      <DiscoverHub />
    </Suspense>
  );
}
