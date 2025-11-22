/**
 * Admin N8N Templates Page - Server Component Wrapper
 * Issue #1608: Frontend Route Protection with E2E Test Compatibility
 */

import { RequireRole } from '@/components/auth/RequireRole';
import { AdminPageClient } from './client';

export default function AdminN8NTemplatesPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <AdminPageClient />
    </RequireRole>
  );
}
