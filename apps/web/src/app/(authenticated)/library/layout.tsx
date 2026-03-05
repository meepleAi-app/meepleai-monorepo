/**
 * Library Section Layout
 * Issue #5039 — User Route Consolidation
 *
 * Mounts LibraryNavConfig here so ALL /library sub-routes
 * (detail pages, wishlist, propose, etc.) get proper nav initialization.
 */

'use client';

import { type ReactNode } from 'react';

import { LibraryNavConfig } from './NavConfig';

export default function LibraryLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <LibraryNavConfig />
      {children}
    </>
  );
}
