/**
 * Agent Test History Page - Server Component Wrapper
 * Issue #3379
 *
 * Displays test results history with filtering and detail view.
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { TestHistoryClient } from './client';

export default function TestHistoryPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <TestHistoryClient />
    </RequireRole>
  );
}
