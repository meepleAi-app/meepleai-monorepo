/**
 * Admin Service Status Dashboard - Server Component Wrapper
 *
 * Issue #2516: Real-time service health monitoring dashboard
 *
 * Features:
 * - Real-time service status monitoring (polling 30s)
 * - Filter by health state (All/Critical/Unhealthy)
 * - Export to JSON/CSV
 * - Toast notifications on state changes
 * - Responsive 3-column grid
 *
 * Security Layers:
 * 1. proxy.ts: Redirects if no session cookie
 * 2. RequireRole: Validates role via getCurrentUser() action
 * 3. Backend API: Final authorization check (403 if role insufficient)
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { ServicesClient } from './services-client';

export default function AdminServicesPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <ServicesClient />
    </RequireRole>
  );
}
