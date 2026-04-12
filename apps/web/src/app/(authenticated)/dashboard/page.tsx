/**
 * Gaming Hub Dashboard
 *
 * Authenticated user dashboard with gaming-focused hub.
 * Uses DashboardClient with 4 hub blocks: Giochi, Sessioni, Agenti AI, Toolkit.
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { DashboardClient } from './DashboardClient';

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
      <DashboardClient />
    </RequireRole>
  );
}
