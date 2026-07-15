/**
 * User Library Page
 * Issue #574 — Wave B.3 brownfield migration
 *
 * Renders LibraryHub (responsive single-tree: hero + tabs + toolbar +
 * LibraryHybridGrid + RecentActivityRail). Mobile/desktop responsive
 * behaviour lives inside the feature components themselves, mirroring Wave
 * B.1 (`/games?tab=library`) and Wave B.2 (`/agents`) pattern.
 *
 * MiniNav tabs + ActionBar are registered by layout.tsx via LibraryNavConfig.
 */

import { Suspense } from 'react';

import { RequireRole } from '@/components/auth/RequireRole';

import { LibraryContent, LibraryLoadingSkeleton } from './_content';

export default function LibraryPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <Suspense fallback={<LibraryLoadingSkeleton />}>
        <LibraryContent />
      </Suspense>
    </RequireRole>
  );
}
