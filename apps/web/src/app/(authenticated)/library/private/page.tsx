/**
 * Private Games List Page
 * Issue #4052: Browse and manage private games
 * Issue #4055: Library section navigation tabs
 *
 * Protected route - requires authentication (User, Editor, or Admin role).
 * Note: Uses force-dynamic to avoid DOMMatrix SSR errors (Issue #4133)
 * Server component - direct imports avoid barrel re-export DOMMatrix issue.
 */

// Disable SSG to prevent DOMMatrix error with react-pdf (Issue #4133)
export const dynamic = 'force-dynamic';

import { RequireRole } from '@/components/auth/RequireRole';
import { LibraryNavTabs } from '@/components/library/LibraryNavTabs';

import PrivateGamesClient from './PrivateGamesClient';

export default function PrivateGamesPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <LibraryNavTabs />
      <PrivateGamesClient />
    </RequireRole>
  );
}
