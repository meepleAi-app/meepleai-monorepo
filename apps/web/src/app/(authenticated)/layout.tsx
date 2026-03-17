/**
 * Authenticated Route Group Layout
 * Carte in Mano — Uses UnifiedShell (card-hand navigation)
 */

import { type ReactNode } from 'react';

import { UnifiedShell } from '@/components/layout/UnifiedShell';
import { getServerUser, isAdmin } from '@/lib/auth';

export default async function AuthenticatedRouteLayout({ children }: { children: ReactNode }) {
  const user = await getServerUser();
  const userIsAdmin = user ? isAdmin(user) : false;

  return <UnifiedShell isAdmin={userIsAdmin}>{children}</UnifiedShell>;
}
