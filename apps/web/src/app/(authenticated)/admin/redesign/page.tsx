/**
 * Admin Dashboard Redesign Preview
 *
 * Temporary route to preview the new dashboard design.
 * Access at: /admin/redesign
 */

import { RequireRole } from '@/components/auth/RequireRole';

import AdminDashboardRedesign from '../page-redesign';

export default function RedesignPreviewPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <AdminDashboardRedesign />
    </RequireRole>
  );
}
