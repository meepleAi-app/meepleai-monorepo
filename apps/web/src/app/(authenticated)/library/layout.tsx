/**
 * Library Section Layout
 * Issue #5039 — User Route Consolidation
 *
 * Pure passthrough — all MiniNav tabs and ActionBar actions
 * are registered by LibraryNavConfig (NavConfig.tsx) to avoid duplication.
 */

'use client';

import { type ReactNode } from 'react';

export default function LibraryLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
