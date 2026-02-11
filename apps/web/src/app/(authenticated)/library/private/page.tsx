/**
 * Private Games List Page
 * Issue #4052: Browse and manage private games
 *
 * Protected route - requires authentication (User, Editor, or Admin role).
 */

'use client';

import { RequireRole } from '@/components/auth/RequireRole';

import PrivateGamesClient from './PrivateGamesClient';

export default function PrivateGamesPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <PrivateGamesClient />
    </RequireRole>
  );
}
