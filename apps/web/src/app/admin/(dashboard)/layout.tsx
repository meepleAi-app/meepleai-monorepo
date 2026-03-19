import { type ReactNode } from 'react';

import { type Metadata } from 'next';

import { PdfProcessingNotifier } from '@/components/admin/layout/PdfProcessingNotifier';
import { RequireRole } from '@/components/auth/RequireRole';
import { AdminShell } from '@/components/layout/AdminShell';

export const metadata: Metadata = {
  title: {
    template: '%s | MeepleAI Admin',
    default: 'Admin Dashboard | MeepleAI',
  },
  description: 'MeepleAI Administration Dashboard',
};

/**
 * Dashboard route group layout.
 * Applies the AdminShell to all pages under /admin/(dashboard)/.
 * Wrapped with RequireRole to enforce admin access.
 */
export default function DashboardLayout({ children }: { children: ReactNode }) {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <PdfProcessingNotifier />
      <AdminShell>{children}</AdminShell>
    </RequireRole>
  );
}
