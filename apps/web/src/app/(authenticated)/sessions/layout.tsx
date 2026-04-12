/**
 * Sessions Section Layout
 * Issue #5045 — Sessions + MiniNav + ActionBar
 *
 * Registers MiniNav tabs and FloatingActionBar for the /sessions section.
 * Tabs: Sessioni attive · Storico
 */

'use client';

import { type ReactNode } from 'react';

import { usePathname, useRouter } from 'next/navigation';

import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';

export default function SessionsLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();

  useMiniNavConfig({
    breadcrumb: 'Sessioni',
    tabs: [
      { id: 'active', label: 'Attive', href: '/sessions' },
      { id: 'history', label: 'Storico', href: '/sessions?tab=history' },
    ],
    activeTabId: pathname?.includes('tab=history') ? 'history' : 'active',
    primaryAction: {
      label: 'Nuova Sessione',
      icon: '▶',
      onClick: () => router.push('/sessions/new'),
    },
  });

  return <>{children}</>;
}
