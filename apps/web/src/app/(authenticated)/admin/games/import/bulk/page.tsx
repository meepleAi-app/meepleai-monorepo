/**
 * Admin Bulk Import Page - Server Component Wrapper
 * Issue #4355: Frontend - Bulk Import Upload UI
 * Epic #4136: Admin Game Import
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { BulkImportJsonUploader } from './client';

export default function AdminBulkImportPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <BulkImportJsonUploader />
    </RequireRole>
  );
}
