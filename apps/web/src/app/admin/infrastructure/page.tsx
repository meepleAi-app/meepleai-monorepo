/**
 * Infrastructure Monitoring Page - Server Component Wrapper
 *
 * Issue #899: Infrastructure monitoring UI with real-time service health,
 * Prometheus metrics, and Grafana dashboard embeds.
 *
 * Features (FASE 2):
 * - Real-time service health matrix (polling 30s)
 * - Advanced metrics charts (CPU, Memory, API requests)
 * - Grafana iframe embeds
 * - Service filtering and sorting
 * - Export functionality (CSV/JSON)
 * - Responsive design
 *
 * Security Layers:
 * 1. middleware.ts: Redirects if no session cookie
 * 2. RequireRole: Validates role via getCurrentUser() action
 * 3. Backend API: Final authorization check (403 if role insufficient)
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { InfrastructureClient } from './infrastructure-client';

export default function InfrastructurePage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <InfrastructureClient />
    </RequireRole>
  );
}
