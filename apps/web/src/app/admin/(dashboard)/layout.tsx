import { type ReactNode } from 'react';

import { type Metadata } from 'next';
import { redirect } from 'next/navigation';

import { PdfProcessingNotifier } from '@/components/admin/layout/PdfProcessingNotifier';
import { RequireRole } from '@/components/auth/RequireRole';
import { AdminShell } from '@/components/layout/AdminShell';
import { readViewModeCookieServer } from '@/lib/view-mode/server';

export const metadata: Metadata = {
  title: {
    template: '%s | MeepleAI Admin',
    default: 'Admin Dashboard | MeepleAI',
  },
  description: 'MeepleAI Administration Dashboard',
};

/**
 * Dashboard route group layout.
 *
 * Guard chain:
 *  1. Server-side: if `meepleai_view_mode` cookie === 'user', redirect to '/'
 *     before the admin shell renders (no flash).
 *  2. Client-side: `RequireRole` enforces admin role (authoritative check).
 *
 * Applies the AdminShell to all pages under /admin/(dashboard)/.
 */
export default async function DashboardLayout({ children }: { children: ReactNode }) {
  const viewMode = await readViewModeCookieServer();
  if (viewMode === 'user') {
    redirect('/');
  }

  return (
    <RequireRole allowedRoles={['Admin']}>
      <PdfProcessingNotifier />
      <AdminShell>{children}</AdminShell>
    </RequireRole>
  );
}
