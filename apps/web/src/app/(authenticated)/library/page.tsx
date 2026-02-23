/**
 * User Library Page
 * Issue #2464, #2613, #2618 — Library management
 * Issue #5042 — Tab-based routing (replaces sub-page navigation)
 * Issue #5167 — Tab rename: Games (personal) / Collection (shared catalog)
 *
 * Handles tabs via ?tab search param:
 *   (default)           → Games      → GamesPageClient      (personal private games)
 *   ?tab=collection     → Collection → CollectionPageClient (shared catalog games)
 *   ?tab=wishlist       → Wishlist
 *
 * MiniNav tabs are registered by layout.tsx (overridden by LibraryNavConfig in page).
 */

import { Suspense } from 'react';

import { RequireRole } from '@/components/auth/RequireRole';

import { LibraryContent, LibraryLoadingSkeleton } from './_content';
import { LibraryNavConfig } from './NavConfig';

export default function LibraryPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <LibraryNavConfig />
      <Suspense fallback={<LibraryLoadingSkeleton />}>
        <LibraryContent />
      </Suspense>
    </RequireRole>
  );
}
