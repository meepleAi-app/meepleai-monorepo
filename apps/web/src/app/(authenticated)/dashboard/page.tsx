/**
 * Dashboard Page (New Design) - Issue #3286
 *
 * New navigation hub design with:
 * - 6 reorderable sections
 * - Grid/List view toggle per section
 * - Global search with filters
 * - Warm Tabletop aesthetic
 *
 * @see docs/features/dashboard-requirements.md
 */

'use client';

export const dynamic = 'force-dynamic';

import { Dashboard } from '@/components/dashboard';
import { Layout } from '@/components/layout';

export default function DashboardPage() {
  return (
    <Layout showActionBar>
      <Dashboard />
    </Layout>
  );
}
