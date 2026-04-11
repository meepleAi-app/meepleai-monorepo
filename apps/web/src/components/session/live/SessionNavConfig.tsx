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

import { usePathname } from 'next/navigation';

import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';

interface SessionNavConfigProps {
  sessionId: string;
}

export function SessionNavConfig({ sessionId }: SessionNavConfigProps) {
  const pathname = usePathname();

  const activeTabId = pathname?.includes('/players')
    ? 'players'
    : pathname?.includes('/photos')
      ? 'photos'
      : pathname?.includes('/scores')
        ? 'scores'
        : pathname?.includes('/agent')
          ? 'agent'
          : 'partita';

  useMiniNavConfig({
    breadcrumb: 'Partita',
    tabs: [
      { id: 'partita', label: 'Partita', href: `/sessions/live/${sessionId}` },
      { id: 'agent', label: 'Chat AI', href: `/sessions/live/${sessionId}/agent` },
      { id: 'scores', label: 'Punteggi', href: `/sessions/live/${sessionId}/scores` },
      { id: 'photos', label: 'Foto', href: `/sessions/live/${sessionId}/photos` },
      { id: 'players', label: 'Giocatori', href: `/sessions/live/${sessionId}/players` },
    ],
    activeTabId,
  });

  return null;
}
