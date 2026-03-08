/**
 * My Proposals Dashboard Page
 * Issue #3669: Phase 8 - Frontend Integration (Task 8.5)
 * Issue #4055: Library section navigation tabs
 * Issue #4056: Add RequireRole to prevent auth flash
 *
 * Dashboard showing user's game proposals with status tracking.
 * Protected route - requires authentication (User, Editor, or Admin role).
 * Note: Uses force-dynamic to avoid DOMMatrix SSR errors (Issue #4133)
 * Server component - direct imports avoid barrel re-export DOMMatrix issue.
 */

// Disable SSG to prevent DOMMatrix error with react-pdf (Issue #4133)
export const dynamic = 'force-dynamic';

import { RequireRole } from '@/components/auth/RequireRole';

import MyProposalsClient from './MyProposalsClient';

export default function MyProposalsPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <MyProposalsClient />
    </RequireRole>
  );
}
