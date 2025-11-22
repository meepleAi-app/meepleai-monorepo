/**
 * Upload Page - Server Component Wrapper
 *
 * Issue #1608: Frontend Route Protection with E2E Test Compatibility
 *
 * Architecture:
 * - Middleware: Blocks unauthenticated users (server-side)
 * - RequireRole: Blocks unauthorized roles (client-side, E2E compatible)
 * - UploadClient: Interactive upload UI
 *
 * Security Layers:
 * 1. middleware.ts: Redirects if no session cookie
 * 2. RequireRole: Validates role via getCurrentUser() action
 * 3. Backend API: Final authorization check (403 if role insufficient)
 *
 * Authorized Roles: Admin, Editor
 */

import { RequireRole } from '@/components/auth/RequireRole';
import { UploadClient } from './upload-client';

export default function UploadPage() {
  return (
    <RequireRole allowedRoles={['Admin', 'Editor']}>
      <UploadClient />
    </RequireRole>
  );
}
