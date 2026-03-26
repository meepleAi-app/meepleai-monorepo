/**
 * Authenticated Route Group Layout
 *
 * Uses UserShell for all authenticated user pages.
 * Admin pages use a separate AdminShell via admin/(dashboard)/layout.tsx.
 */

import { type ReactNode } from 'react';

import { UserShell } from '@/components/layout/UserShell';

export default async function AuthenticatedRouteLayout({ children }: { children: ReactNode }) {
  return <UserShell>{children}</UserShell>;
}
