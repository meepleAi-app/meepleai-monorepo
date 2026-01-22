/**
 * Admin Pending Approvals Page - Server Component Wrapper
 * Issue #2514: SharedGameCatalog Approval Workflow
 *
 * Security:
 * - RequireRole: Admin only (approval/rejection is admin-only operation)
 * - All operations use CQRS commands/queries via sharedGamesClient
 * - Session validated at middleware level
 */

import { Metadata } from 'next';

import { RequireRole } from '@/components/auth/RequireRole';

import { PendingApprovalsClient } from './client';

export const metadata: Metadata = {
  title: 'Pending Approvals | MeepleAI Admin',
  description: 'Review and approve games submitted for publication in the shared game catalog',
};

export default function PendingApprovalsPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <PendingApprovalsClient />
    </RequireRole>
  );
}
