/**
 * SessionNavConfig
 *
 * Game Night Improvvisata — Task 15
 *
 * Registers MiniNav tabs for the live session section.
 * Renders null — purely declarative navigation config.
 *
 * Tabs: Partita · Chat AI · Punteggi · Foto · Giocatori
 */

'use client';

import { useEffect } from 'react';

import { Bot, Camera, LayoutDashboard, Trophy, Users } from 'lucide-react';

import { useSetNavConfig } from '@/context/NavigationContext';

interface SessionNavConfigProps {
  sessionId: string;
}

export function SessionNavConfig({ sessionId }: SessionNavConfigProps) {
  const setNavConfig = useSetNavConfig();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        {
          id: 'partita',
          label: 'Partita',
          href: `/sessions/live/${sessionId}`,
          icon: LayoutDashboard,
        },
        {
          id: 'agent',
          label: 'Chat AI',
          href: `/sessions/live/${sessionId}/agent`,
          icon: Bot,
        },
        {
          id: 'scores',
          label: 'Punteggi',
          href: `/sessions/live/${sessionId}/scores`,
          icon: Trophy,
        },
        {
          id: 'photos',
          label: 'Foto',
          href: `/sessions/live/${sessionId}/photos`,
          icon: Camera,
        },
        {
          id: 'players',
          label: 'Giocatori',
          href: `/sessions/live/${sessionId}/players`,
          icon: Users,
        },
      ],
    });
  }, [sessionId, setNavConfig]);

  return null;
}
