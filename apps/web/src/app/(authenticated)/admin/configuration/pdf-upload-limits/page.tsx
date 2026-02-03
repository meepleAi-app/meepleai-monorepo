/**
 * PDF Upload Limits Configuration Page - Server Component Wrapper
 * Issue #3078: Admin UI - PDF Limits Configuration
 */

import { RequireRole } from '@/components/auth/RequireRole';

import { PdfUploadLimitsClient } from './client';

export default function PdfUploadLimitsPage() {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <PdfUploadLimitsClient />
    </RequireRole>
  );
}
