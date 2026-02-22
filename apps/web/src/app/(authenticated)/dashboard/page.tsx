/**
 * Gaming Hub Dashboard - Issue #4584
 * Epic #4575: Gaming Hub Dashboard - Phase 2
 *
 * Authenticated user dashboard with gaming-focused hub
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { GamingHubClient } from '../gaming-hub-client';
import { DashboardNavConfig } from './NavConfig';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Gaming Hub | MeepleAI',
  description:
    'Your personal board game hub. Track sessions, manage your collection, and view your gaming statistics.',
  openGraph: {
    title: 'Gaming Hub | MeepleAI',
    description: 'Your personal board game hub.',
    type: 'website',
  },
};

export const dynamic = 'force-dynamic';

export default function GamingHubDashboardPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <DashboardNavConfig />
      <GamingHubClient />
    </RequireRole>
  );
}
