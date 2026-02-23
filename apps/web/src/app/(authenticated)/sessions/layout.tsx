/**
 * Sessions Section Layout
 * Issue #5045 — Sessions + MiniNav + ActionBar
 *
 * Registers MiniNav tabs and FloatingActionBar for the /sessions section.
 * Tabs: Sessioni attive · Storico
 */

'use client';

import { type ReactNode, useEffect } from 'react';

import { Clock, History, Play } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function SessionsLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const setNavConfig = useSetNavConfig();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'active', label: 'Attive', href: '/sessions', icon: Clock },
        { id: 'history', label: 'Storico', href: '/sessions?tab=history', icon: History },
      ],
      actionBar: [
        {
          id: 'new-session',
          label: 'Nuova Sessione',
          icon: Play,
          variant: 'primary',
          onClick: () => {
            router.push('/sessions/new');
          },
        },
      ],
    });
  }, [setNavConfig, router]);

  return <>{children}</>;
}
