/**
 * Admin Dashboard - Server Component Wrapper
 *
 * Issue #874: Enhanced centralized dashboard with metrics + activity feed
 *
 * Features (FASE 1):
 * - 16 real-time metrics (polling 30s)
 * - Activity feed (last 10 events)
 * - AdminLayout with navigation
 * - Performance optimized (<1s load, <2s TTI)
 *
 * Security Layers:
 * 1. middleware.ts: Redirects if no session cookie
 * 2. RequireRole: Validates role via getCurrentUser() action
 * 3. Backend API: Final authorization check (403 if role insufficient)
 */

import { RequireRole } from '@/components/auth/RequireRole';
import { DashboardClient } from './dashboard-client';

export default function AdminPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <DashboardClient />
    </RequireRole>
  );
}
