/**
 * Admin Testing Dashboard Page - Server Component Wrapper
 * Issue #844: UI/UX Automated Testing Roadmap 2025
 *
 * Consolidated dashboard for automated testing results:
 * - Accessibility testing (Issue #841)
 * - Performance testing (Issue #842)
 * - E2E coverage expansion (Issue #843)
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { TestingDashboardClient } from './client';

export default function AdminTestingPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <TestingDashboardClient />
    </RequireRole>
  );
}
