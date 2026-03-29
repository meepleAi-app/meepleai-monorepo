/**
 * Gaming Hub Dashboard
 *
 * Authenticated user dashboard with gaming-focused hub.
 * Uses DashboardClient with HeroBanner, QuickActionsRow,
 * and MeepleCard-based game sections.
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { DashboardClient } from './dashboard-client';
import { DashboardMobile } from './dashboard-mobile';

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
      <div className="lg:hidden">
        <DashboardMobile />
      </div>
      <div className="hidden lg:block">
        <DashboardClient />
      </div>
    </RequireRole>
  );
}
