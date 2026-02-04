/**
 * Authenticated Route Group Layout
 * Issue #3479 - Layout System v2: Unified Layout
 *
 * Applies AuthenticatedLayout to all pages in (authenticated) group:
 * - /dashboard
 * - /library
 * - /toolkit
 * - /agent
 * - /admin (all admin routes)
 *
 * Features:
 * - UnifiedHeader with desktop nav + settings + notifications + user menu
 * - UnifiedActionBar (mobile-only): bottom nav with integrated FAB + context actions
 * - Breadcrumb navigation (desktop)
 * - Dark mode support
 * - Mobile-first responsive design
 */

'use client';

import { ReactNode } from 'react';

import { AuthenticatedLayout } from '@/components/layouts/AuthenticatedLayout';

export default function AuthenticatedRouteLayout({ children }: { children: ReactNode }) {
  return (
    <AuthenticatedLayout>
      {children}
    </AuthenticatedLayout>
  );
}
