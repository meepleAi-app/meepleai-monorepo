/**
 * Admin Reports Page - Server Component Wrapper
 * Issue #920: Report Builder UI
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { ReportsPageClient } from './client';

export const metadata = {
  title: 'Reports | Admin | MeepleAI',
  description: 'Generate and schedule system reports',
};

export default function AdminReportsPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <ReportsPageClient />
    </RequireRole>
  );
}
