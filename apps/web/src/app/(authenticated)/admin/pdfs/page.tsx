/**
 * Admin PDF Storage Management Hub - Server Component Wrapper
 * PDF Storage Management Hub: Correct 7-state status, storage dashboard, admin actions.
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { PdfAdminClient } from './client';

export default function AdminPdfsPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <PdfAdminClient />
    </RequireRole>
  );
}
