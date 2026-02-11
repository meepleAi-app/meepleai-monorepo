/**
 * Admin Usage Stats Page - Server Component Wrapper
 * Issue #3719: App Usage Stats (DAU/MAU, retention, feature adoption, geo)
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { UsageStatsClient } from './client';

export default function UsageStatsPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <UsageStatsClient />
    </RequireRole>
  );
}
