/**
 * GameNightsNavConfig — MiniNav + ActionBar for /game-nights
 * Issue #33 — P3 Game Night Frontend
 *
 * Tabs: Prossime · Le Mie
 * ActionBar: Nuova Serata (primary)
 */

'use client';

import { useEffect } from 'react';

import { Calendar, Plus, User } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function GameNightsNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        {
          id: 'upcoming',
          label: 'Prossime',
          href: '/game-nights',
          icon: Calendar,
        },
        {
          id: 'mine',
          label: 'Le Mie',
          href: '/game-nights?tab=mine',
          icon: User,
        },
      ],
      actionBar: [
        {
          id: 'new-game-night',
          label: 'Nuova Serata',
          icon: Plus,
          variant: 'primary',
          onClick: () => router.push('/game-nights/new'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
