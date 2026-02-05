/**
 * Agent Metrics Dashboard Page - Server Component Wrapper
 * Issue #3382
 *
 * Admin dashboard for viewing agent usage, costs, and accuracy metrics.
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { MetricsClient } from './client';

export default function AgentMetricsPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <MetricsClient />
    </RequireRole>
  );
}
