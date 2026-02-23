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
