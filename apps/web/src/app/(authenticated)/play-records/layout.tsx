/**
 * Play Records Section Layout
 * Issue #5039 — User Route Consolidation
 * Issue #5045 — Play Records + ActionBar
 *
 * Registers MiniNav tabs and FloatingActionBar for the /play-records section.
 */

'use client';

import { type ReactNode } from 'react';

import { usePathname, useRouter } from 'next/navigation';

import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';

export default function PlayRecordsLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();

  useMiniNavConfig({
    breadcrumb: 'Partite',
    tabs: [
      { id: 'records', label: 'Partite', href: '/play-records' },
      { id: 'stats', label: 'Statistiche', href: '/play-records?tab=stats' },
    ],
    activeTabId: pathname?.includes('tab=stats') ? 'stats' : 'records',
    primaryAction: {
      label: 'Nuova Partita',
      icon: '＋',
      onClick: () => router.push('/play-records/new'),
    },
  });

  return <>{children}</>;
}
