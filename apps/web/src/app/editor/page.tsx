'use client';

/**
 * Editor Page - Client Component with Code Splitting
 *
 * Issue #1608: Frontend Route Protection with E2E Test Compatibility
 * Issue #2245: Code splitting for performance optimization
 *
 * Architecture:
 * - Middleware: Blocks unauthenticated users (server-side)
 * - RequireRole: Blocks unauthorized roles (client-side, E2E compatible)
 * - EditorClient: Interactive editor UI (lazy loaded)
 *
 * Security Layers:
 * 1. middleware.ts: Redirects if no session cookie
 * 2. RequireRole: Validates role via getCurrentUser() action
 * 3. Backend API: Final authorization check (403 if role insufficient)
 *
 * Performance:
 * - EditorClient lazy loaded with dynamic() (Issue #2245)
 * - Reduces initial bundle size by ~32KB
 * - Loads on-demand when route is accessed
 * - Client Component required for dynamic() in Next.js 16
 *
 * Authorized Roles: Admin, Editor
 */

import dynamic from 'next/dynamic';
import { RequireRole } from '@/components/auth/RequireRole';

// Code splitting: lazy load EditorClient (Issue #2245)
const EditorClient = dynamic(
  () => import('./editor-client').then(mod => ({ default: mod.EditorClient })),
  {
    loading: () => (
      <div className="flex items-center justify-center h-screen">Caricamento editor...</div>
    ),
  }
);

export default function EditorPage() {
  return (
    <RequireRole allowedRoles={['Admin', 'Editor']}>
      <EditorClient />
    </RequireRole>
  );
}
