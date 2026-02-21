/**
 * Alert Management Page (Issue #921)
 *
 * Server Component wrapper for the alert management client page.
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { AlertsPageClient } from './client';

export const metadata = {
  title: 'Alert Management | Admin',
  description: 'Monitor and resolve system alerts from Prometheus AlertManager',
};

export default function AlertsPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <AlertsPageClient />
    </RequireRole>
  );
}
