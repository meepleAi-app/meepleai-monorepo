/**
 * Authenticated Route Group Layout
 *
 * Uses LayoutSwitch to conditionally render AlphaShell (alpha-layout flag)
 * or UnifiedShell (default). Both shells are server-rendered and passed as
 * slots so the client-side flag check can swap between them.
 */

import { type ReactNode } from 'react';

import { AlphaShell, LayoutSwitch } from '@/components/layout/alpha';
import { UnifiedShell } from '@/components/layout/UnifiedShell';
import { getServerUser, isAdmin } from '@/lib/auth';

export default async function AuthenticatedRouteLayout({ children }: { children: ReactNode }) {
  const user = await getServerUser();
  const userIsAdmin = user ? isAdmin(user) : false;

  return (
    <LayoutSwitch
      alphaSlot={<AlphaShell isAdmin={userIsAdmin}>{children}</AlphaShell>}
      defaultSlot={<UnifiedShell isAdmin={userIsAdmin}>{children}</UnifiedShell>}
    />
  );
}
