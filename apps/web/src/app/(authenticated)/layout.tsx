/**
 * Authenticated Route Group Layout
 * Issue #5035 - Layout System v3: AppShell (Unified RSC)
 *
 * Uses AppShell (RSC) which reads sidebar cookie on the server
 * to prevent sidebar flash. Delegates to AppShellClient for
 * the full 3-tier navigation system.
 *
 * Pages register their nav config via useSetNavConfig() in layout.tsx or page.tsx.
 */

import { type ReactNode } from 'react';

import { AppShell } from '@/components/layout/AppShell';

export default function AuthenticatedRouteLayout({ children }: { children: ReactNode }) {
  return <AppShell>{children}</AppShell>;
}
