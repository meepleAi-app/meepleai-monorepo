/**
 * My Proposals Dashboard Page
 * Issue #3669: Phase 8 - Frontend Integration (Task 8.5)
 * Issue #4056: Add RequireRole to prevent auth flash
 *
 * Dashboard showing user's game proposals with status tracking.
 * Protected route - requires authentication (User, Editor, or Admin role).
 */

'use client';

import { RequireRole } from '@/components/auth/RequireRole';

import MyProposalsClient from './MyProposalsClient';

export default function MyProposalsPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <MyProposalsClient />
    </RequireRole>
  );
}
