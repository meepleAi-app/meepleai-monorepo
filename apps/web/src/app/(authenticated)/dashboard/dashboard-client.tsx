/**
 * Dashboard Client Component - Issue #3975
 *
 * Client-side dashboard hub with aggregated data from `/api/v1/dashboard`
 *
 * Features:
 * - Multi-section overview hub
 * - Hero stats, active sessions, library snapshot, activity feed
 * - Responsive layout (mobile/tablet/desktop)
 * - Error boundaries for partial failures
 *
 * @see Epic #3901 - Dashboard Hub Core (MVP)
 * @see docs/07-frontend/dashboard-overview-hub.md
 */

'use client';

import { DashboardHub } from '@/components/dashboard/DashboardHub';
import { Layout } from '@/components/layout';

export function DashboardClient() {
  return (
    <Layout showActionBar>
      <DashboardHub />
    </Layout>
  );
}
