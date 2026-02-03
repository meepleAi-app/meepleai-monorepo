/**
 * Authenticated Route Group Layout
 * Issue #3479 - Navbar consistency across authenticated pages
 *
 * Applies PublicLayout to all pages in (authenticated) group:
 * - /dashboard
 * - /library
 * - /toolkit
 * - /agent
 *
 * Features:
 * - UnifiedHeader with user context (internally managed)
 * - BottomNav for mobile
 * - Responsive container
 * - Dark mode support
 * - Consistent navigation with public pages
 */

'use client';

import { ReactNode } from 'react';

import { PublicLayout } from '@/components/layouts/PublicLayout';

export default function AuthenticatedLayout({ children }: { children: ReactNode }) {
  console.log('[AuthenticatedLayout] Rendering with PublicLayout');
  return (
    <PublicLayout showNewsletter={false}>
      {children}
    </PublicLayout>
  );
}
