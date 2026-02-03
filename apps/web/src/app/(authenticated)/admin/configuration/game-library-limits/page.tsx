/**
 * Game Library Limits Configuration Page - Server Component Wrapper
 * Issue #2444: Admin UI - Configure Game Library Tier Limits
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { GameLibraryLimitsClient } from './client';

export default function GameLibraryLimitsPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <GameLibraryLimitsClient />
    </RequireRole>
  );
}
