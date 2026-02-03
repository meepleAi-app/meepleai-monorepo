/**
 * Dashboard Page (Server Component) - Issue #3307
 *
 * Server-side entry point for the dashboard hub with:
 * - SEO metadata configuration
 * - Role-based access control (RequireRole)
 * - Client component delegation
 *
 * @see docs/features/dashboard-requirements.md
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { DashboardClient } from './dashboard-client';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Dashboard | MeepleAI',
  description:
    'Your personal board game hub. View your library, track activity, manage wishlists, and get AI-powered game recommendations.',
  openGraph: {
    title: 'Dashboard | MeepleAI',
    description:
      'Your personal board game hub. View your library, track activity, manage wishlists, and get AI-powered game recommendations.',
    type: 'website',
  },
};

export const dynamic = 'force-dynamic';

export default function DashboardPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <DashboardClient />
    </RequireRole>
  );
}
