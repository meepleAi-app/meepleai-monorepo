/**
 * Live Session Layout — /sessions/live/[sessionId]
 *
 * Game Night Improvvisata — Task 15 / Task 17
 *
 * Wraps live session pages with session-specific MiniNav tabs:
 * Partita · Chat AI · Punteggi · Foto · Giocatori
 *
 * Includes a back button to return to the dashboard/play tab.
 * Context bar callbacks are wired to the overlay store (Task 17).
 */

'use client';

import { type ReactNode, use } from 'react';

import { ArrowLeft } from 'lucide-react';
import Link from 'next/link';

import { ContextBarRegistrar } from '@/components/layout/ContextBar';
import { SessionNavConfig } from '@/components/session/live/SessionNavConfig';
import { LiveSessionContextBarConnected } from '@/components/session/LiveSessionContextBarConnected';
import { OverlayHybrid } from '@/components/ui/overlays';

interface LiveSessionLayoutProps {
  children: ReactNode;
  params: Promise<{ sessionId: string }>;
}

export default function LiveSessionLayout({ children, params }: LiveSessionLayoutProps) {
  const { sessionId } = use(params);

  return (
    <>
      <ContextBarRegistrar alwaysVisible>
        <LiveSessionContextBarConnected sessionId={sessionId} />
      </ContextBarRegistrar>
      <div className="flex items-center gap-2 px-4 py-2 border-b border-border/40">
        <Link
          href="/library"
          className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors"
          aria-label="Torna alla dashboard"
        >
          <ArrowLeft className="w-4 h-4" />
          <span>Torna al Play</span>
        </Link>
      </div>
      <SessionNavConfig sessionId={sessionId} />
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
