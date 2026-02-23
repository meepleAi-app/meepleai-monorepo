/**
 * Dashboard Layout
 * Issue #5041 — Dashboard / Home
 *
 * NavConfig is registered by DashboardNavConfig in page.tsx.
 * No MiniNav (dashboard is the global entry point).
 */

import { type ReactNode } from 'react';

export default function DashboardLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
