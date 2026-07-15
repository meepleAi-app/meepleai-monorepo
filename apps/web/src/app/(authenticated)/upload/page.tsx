'use client';

/**
 * Upload Page - Client Component with Code Splitting
 *
 * Issue #1608: Frontend Route Protection with E2E Test Compatibility
 * Issue #2245: Code splitting for performance optimization
 *
 * Architecture:
 * - Middleware: Blocks unauthenticated users (server-side)
 * - RequireRole: Blocks unauthorized roles (client-side, E2E compatible)
 * - UploadClient: Interactive upload UI (lazy loaded)
 *
 * Security Layers:
 * 1. proxy.ts: Redirects if no session cookie
 * 2. RequireRole: Validates role via getCurrentUser() action
 * 3. Backend API: Final authorization check (403 if role insufficient)
 *
 * Performance:
 * - UploadClient lazy loaded with dynamic() (Issue #2245)
 * - Reduces initial bundle size by ~28KB
 * - Client Component required for dynamic() in Next.js 16
 *
 * Authorized Roles: Admin, Editor
 */

import dynamic from 'next/dynamic';

import { RequireRole } from '@/components/auth/RequireRole';

// Code splitting: lazy load UploadClient (Issue #2245)
const UploadClient = dynamic(
  () => import('./upload-client').then(mod => ({ default: mod.UploadClient })),
  {
    loading: () => (
      <div className="flex items-center justify-center h-screen">Caricamento upload...</div>
    ),
  }
);

export default function UploadPage() {
  return (
    <RequireRole allowedRoles={['Admin', 'Editor']}>
      <UploadClient />
    </RequireRole>
  );
}
