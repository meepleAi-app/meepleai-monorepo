/**
 * Admin Sessions Monitoring Page - Server Component Wrapper
 * Gap Analysis: UI for GET /api/v1/admin/sessions
 *
 * Security:
 * - RequireRole: Admin only
 * - All operations use CQRS commands/queries via adminClient
 * - Session validated at middleware level
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { SessionsMonitoringClient } from './client';

export const metadata: Metadata = {
  title: 'Sessions Monitoring | MeepleAI Admin',
  description: 'Monitor and manage active user sessions across the platform',
};

export default function SessionsMonitoringPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <SessionsMonitoringClient />
    </RequireRole>
  );
}
