/**
 * Play Records Section Layout
 * Issue #5039 — User Route Consolidation
 * Issue #5045 — Play Records + ActionBar
 *
 * Registers MiniNav tabs and FloatingActionBar for the /play-records section.
 */

'use client';

import { type ReactNode, useEffect } from 'react';

import { BarChart2, History, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function PlayRecordsLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const setNavConfig = useSetNavConfig();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'records', label: 'Partite', href: '/play-records', icon: History },
        { id: 'stats', label: 'Statistiche', href: '/play-records?tab=stats', icon: BarChart2 },
      ],
      actionBar: [
        {
          id: 'new-record',
          label: 'Nuova Partita',
          icon: Plus,
          variant: 'primary',
          onClick: () => {
            router.push('/play-records/new');
          },
        },
      ],
    });
  }, [setNavConfig, router]);

  return <>{children}</>;
}
