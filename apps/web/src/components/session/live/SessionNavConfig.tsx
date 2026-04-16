/**
 * SessionNavConfig
 *
 * Game Night Improvvisata — Task 15
 *
 * Renders PageHeader with tabs for the live session section.
 *
 * Tabs: Partita · Chat AI · Punteggi · Foto · Giocatori
 */

'use client';

import { PageHeader } from '@/components/layout/PageHeader';

interface SessionNavConfigProps {
  sessionId: string;
  activeTabId?: string;
}

export function SessionNavConfig({ sessionId, activeTabId = 'partita' }: SessionNavConfigProps) {
  return (
    <PageHeader
      title="Sessione Live"
      parentHref="/sessions"
      parentLabel="Sessioni"
      tabs={[
        {
          id: 'partita',
          label: 'Partita',
          href: `/sessions/live/${sessionId}`,
        },
        {
          id: 'agent',
          label: 'Chat AI',
          href: `/sessions/live/${sessionId}/agent`,
        },
        {
          id: 'scores',
          label: 'Punteggi',
          href: `/sessions/live/${sessionId}/scores`,
        },
        {
          id: 'photos',
          label: 'Foto',
          href: `/sessions/live/${sessionId}/photos`,
        },
        {
          id: 'players',
          label: 'Giocatori',
          href: `/sessions/live/${sessionId}/players`,
        },
      ]}
      activeTabId={activeTabId}
    />
  );
}
