/**
 * Admin Audit Log Page - Server Component Wrapper
 * Issue #3691: Audit Log System
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { AuditLogClient } from './client';

export default function AdminAuditLogPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <AuditLogClient />
    </RequireRole>
  );
}
