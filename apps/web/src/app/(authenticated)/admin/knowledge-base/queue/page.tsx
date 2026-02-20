/**
 * Admin KB Processing Queue - Server Component Wrapper
 * Issue #4892
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { ProcessingQueueClient } from './client';

export default function AdminKBQueuePage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <ProcessingQueueClient />
    </RequireRole>
  );
}
