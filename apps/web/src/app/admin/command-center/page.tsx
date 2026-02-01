/**
 * Admin Command Center Page
 *
 * A distinctive "Mission Control" dashboard for admin monitoring.
 * Dark theme with high-contrast data visualization.
 *
 * Issue #3286 - Admin Dashboard Redesign
 */

import { RequireRole } from '@/components/auth/RequireRole';
import { CommandCenterDashboard } from '@/components/admin/command-center';

export const metadata = {
  title: 'Command Center | MeepleAI Admin',
  description: 'Mission Control dashboard for system monitoring and administration',
};

export default function CommandCenterPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <CommandCenterDashboard />
    </RequireRole>
  );
}
