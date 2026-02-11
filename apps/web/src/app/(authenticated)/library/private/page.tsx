/**
 * Private Games List Page
 * Issue #4052: Browse and manage private games
 * Issue #4055: Library section navigation tabs
 *
 * Protected route - requires authentication (User, Editor, or Admin role).
 */

'use client';

import { RequireRole } from '@/components/auth/RequireRole';
import { LibraryNavTabs } from '@/components/library';

import PrivateGamesClient from './PrivateGamesClient';

export default function PrivateGamesPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <LibraryNavTabs />
      <PrivateGamesClient />
    </RequireRole>
  );
}
