/**
 * Live Session Layout — /sessions/live/[sessionId]
 *
 * Game Night Improvvisata — Task 15
 *
 * Wraps live session pages with session-specific MiniNav tabs:
 * Partita · Chat AI · Punteggi · Foto · Giocatori
 *
 * Includes a back button to return to the dashboard/play tab.
 */

'use client';

import { type ReactNode, use } from 'react';

import { ArrowLeft } from 'lucide-react';
import Link from 'next/link';

import { ContextBarRegistrar } from '@/components/layout/ContextBar';
import { SessionNavConfig } from '@/components/session/live/SessionNavConfig';
import { LiveSessionContextBar } from '@/components/session/LiveSessionContextBar';

interface LiveSessionLayoutProps {
  children: ReactNode;
  params: Promise<{ sessionId: string }>;
}

export default function LiveSessionLayout({ children, params }: LiveSessionLayoutProps) {
  const { sessionId } = use(params);

  return (
    <>
      <ContextBarRegistrar alwaysVisible>
        <LiveSessionContextBar />
      </ContextBarRegistrar>
      <div className="flex items-center gap-2 px-4 py-2 border-b border-border/40">
        <Link
          href="/dashboard"
          className="flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors"
          aria-label="Torna alla dashboard"
        >
          <ArrowLeft className="w-4 h-4" />
          <span>Torna al Play</span>
        </Link>
      </div>
      <SessionNavConfig sessionId={sessionId} />
      {children}
    </>
  );
}
