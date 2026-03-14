/**
 * Authenticated Route Group Layout
 * Carte in Mano — Uses UnifiedShell (card-hand navigation)
 */

import { type ReactNode } from 'react';

import { UnifiedShell } from '@/components/layout/UnifiedShell';

export default function AuthenticatedRouteLayout({ children }: { children: ReactNode }) {
  return <UnifiedShell>{children}</UnifiedShell>;
}
