/**
 * Admin Hub - Server Component
 *
 * Root admin page at /admin. Renders the Admin Hub dashboard
 * that consolidates all admin sections into a single tabbed view.
 *
 * Replaces the legacy admin-client.tsx with a modern, card-based hub
 * using MeepleCard design language and entity color system.
 */

import type { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { AdminHubClient } from './hub-client';

export const metadata: Metadata = {
  title: 'Admin Hub | MeepleAI',
  description: 'Pannello di controllo amministrativo - panoramica completa del sistema',
};

export default function AdminPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <AdminHubClient />
    </RequireRole>
  );
}
