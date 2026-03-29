/**
 * User Library Page
 * Issue #2464, #2613, #2618 — Library management
 * Issue #5042 — Tab-based routing (replaces sub-page navigation)
 * Issue #5167 — Tab rename: Games (personal) / Collection (shared catalog)
 *
 * Mobile: renders LibraryMobile (segmented control + grid)
 * Desktop: renders LibraryContent (tab-based layout with sidebar)
 *
 * MiniNav tabs + ActionBar are registered by layout.tsx via LibraryNavConfig.
 */

import { Suspense } from 'react';

import { RequireRole } from '@/components/auth/RequireRole';

import { LibraryContent, LibraryLoadingSkeleton } from './_content';
import { LibraryMobile } from './library-mobile';

export default function LibraryPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      {/* Mobile: segmented control + grid */}
      <LibraryMobile />
      {/* Desktop: tab-based layout (hidden on mobile by LibraryContent internals) */}
      <Suspense fallback={<LibraryLoadingSkeleton />}>
        <div className="hidden lg:block">
          <LibraryContent />
        </div>
      </Suspense>
    </RequireRole>
  );
}
