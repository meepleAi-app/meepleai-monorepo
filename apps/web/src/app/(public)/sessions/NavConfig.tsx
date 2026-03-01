'use client';

/**
 * SessionsNavConfig — Registers MiniNav tabs + ActionBar actions for /sessions
 * Issue #5045 — Sessions + Play Records MiniNav + ActionBar
 *
 * Tabs: Active · History · Stats
 * ActionBar: New Session (primary) · Import · Filter
 *
 * Include in sessions/page.tsx:
 *   <SessionsNavConfig />
 */

import { useEffect } from 'react';

import { BarChart, Filter, Play, Upload } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function SessionsNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'active',  label: 'Active',  href: '/sessions' },
        { id: 'history', label: 'History', href: '/sessions/history' },
        { id: 'stats',   label: 'Stats',   href: '/sessions?tab=stats' },
      ],
      actionBar: [
        {
          id: 'new-session',
          label: 'New Session',
          icon: Play,
          variant: 'primary',
          onClick: () => router.push('/sessions/new'),
        },
        {
          id: 'import',
          label: 'Import',
          icon: Upload,
          onClick: () => router.push('/sessions?action=import'),
        },
        {
          id: 'filter',
          label: 'Filter',
          icon: Filter,
          onClick: () => router.push('/sessions?action=filter'),
        },
        {
          id: 'stats',
          label: 'Stats',
          icon: BarChart,
          onClick: () => router.push('/sessions?tab=stats'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
