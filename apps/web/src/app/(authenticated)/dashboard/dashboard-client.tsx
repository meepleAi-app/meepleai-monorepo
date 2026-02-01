/**
 * Dashboard Client Component - Issue #3307
 *
 * Client-side dashboard with:
 * - 6 reorderable sections
 * - Grid/List view toggle per section
 * - Global search with filters
 * - Warm Tabletop aesthetic
 *
 * @see docs/features/dashboard-requirements.md
 */

'use client';

import { Dashboard } from '@/components/dashboard';
import { Layout } from '@/components/layout';

export function DashboardClient() {
  return (
    <Layout showActionBar>
      <Dashboard />
    </Layout>
  );
}
