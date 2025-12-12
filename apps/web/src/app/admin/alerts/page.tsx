/**
 * Alert Management Page (Issue #921)
 *
 * Server Component wrapper for the alert management client page.
 * Renders the client component with AdminLayout.
 */

import { AdminLayout } from '@/components/admin/AdminLayout';
import { AlertsPageClient } from './client';

export const metadata = {
  title: 'Alert Management | Admin',
  description: 'Monitor and resolve system alerts from Prometheus AlertManager',
};

export default function AlertsPage() {
  return (
    <AdminLayout>
      <AlertsPageClient />
    </AdminLayout>
  );
}
