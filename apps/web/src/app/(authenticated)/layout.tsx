/**
 * Authenticated Route Group Layout
 * Issue #5035 - Layout System v3: LayoutShell (Concept 4 Floating)
 *
 * Applies LayoutShell to all pages in (authenticated) group.
 * Concept 4: TopNavbar (sticky) + MiniNav (context) + FloatingActionBar (pill)
 *
 * Pages register their nav config via useSetNavConfig() in layout.tsx or page.tsx.
 */

import { ReactNode } from 'react';

import { LayoutShell } from '@/components/layout/LayoutShell';

export default function AuthenticatedRouteLayout({ children }: { children: ReactNode }) {
  return (
    <LayoutShell>
      {children}
    </LayoutShell>
  );
}
