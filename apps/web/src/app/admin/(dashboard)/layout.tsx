import { type ReactNode } from 'react';

import { type Metadata } from 'next';

import { AdminShell } from '@/components/admin/layout/AdminShell';
import { RequireRole } from '@/components/auth/RequireRole';

export const metadata: Metadata = {
  title: {
    template: '%s | MeepleAI Admin',
    default: 'Admin Dashboard | MeepleAI',
  },
  description: 'MeepleAI Administration Dashboard',
};

/**
 * Dashboard route group layout.
 * Applies the unified AdminShell (TopNav + ContextualSidebar)
 * to all pages under /admin/(dashboard)/.
 * Wrapped with RequireRole to enforce admin access.
 */
export default function DashboardLayout({ children }: { children: ReactNode }) {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <AdminShell>
        {children}
      </AdminShell>
    </RequireRole>
  );
}
