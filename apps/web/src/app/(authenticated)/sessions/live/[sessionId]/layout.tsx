/**
 * Live Session Layout — /sessions/live/[sessionId]
 *
 * Game Night Improvvisata — Task 15 / Task 17
 *
 * Wraps live session pages with session-specific PageHeader tabs:
 * Partita · Chat AI · Punteggi · Foto · Giocatori
 *
 * Context bar callbacks are wired to the overlay store (Task 17).
 */

'use client';

import { type ReactNode, use } from 'react';

import { usePathname } from 'next/navigation';

import { ContextBarRegistrar } from '@/components/layout/ContextBar';
import { PageHeader } from '@/components/layout/PageHeader';
import { LiveSessionContextBarConnected } from '@/components/session/LiveSessionContextBarConnected';
import { OverlayHybrid } from '@/components/ui/overlays';

interface LiveSessionLayoutProps {
  children: ReactNode;
  params: Promise<{ sessionId: string }>;
}

function getActiveTabId(pathname: string): string {
  if (pathname.endsWith('/agent')) return 'agent';
  if (pathname.endsWith('/scores')) return 'scores';
  if (pathname.endsWith('/photos')) return 'photos';
  if (pathname.endsWith('/players')) return 'players';
  return 'partita';
}

export default function LiveSessionLayout({ children, params }: LiveSessionLayoutProps) {
  const { sessionId } = use(params);
  const pathname = usePathname();
  const activeTabId = getActiveTabId(pathname);

  return (
    <>
      <ContextBarRegistrar alwaysVisible>
        <LiveSessionContextBarConnected sessionId={sessionId} />
      </ContextBarRegistrar>
      <div className="px-4 py-4">
        <PageHeader
          title="Partita"
          parentHref="/sessions"
          parentLabel="Sessioni"
          tabs={[
            { id: 'partita', label: 'Partita', href: `/sessions/live/${sessionId}` },
            { id: 'agent', label: 'Chat AI', href: `/sessions/live/${sessionId}/agent` },
            { id: 'scores', label: 'Punteggi', href: `/sessions/live/${sessionId}/scores` },
            { id: 'photos', label: 'Foto', href: `/sessions/live/${sessionId}/photos` },
            { id: 'players', label: 'Giocatori', href: `/sessions/live/${sessionId}/players` },
          ]}
          activeTabId={activeTabId}
        />
      </div>
      {children}

      <OverlayHybrid enableDeepLink>
        {({ entityType, entityId }) => (
          <div className="p-4">
            <p className="text-sm text-muted-foreground">
              {entityType}: {entityId}
            </p>
          </div>
        )}
      </OverlayHybrid>
    </>
  );
}
