/**
 * Live Session Layout — /sessions/live/[sessionId]
 *
 * Game Night Improvvisata — Task 15
 *
 * Wraps live session pages with session-specific MiniNav tabs:
 * Partita · Chat AI · Punteggi · Foto · Giocatori
 */

'use client';

import { type ReactNode, use } from 'react';

import { SessionNavConfig } from '@/components/session/live/SessionNavConfig';

interface LiveSessionLayoutProps {
  children: ReactNode;
  params: Promise<{ sessionId: string }>;
}

export default function LiveSessionLayout({ children, params }: LiveSessionLayoutProps) {
  const { sessionId } = use(params);

  return (
    <>
      <SessionNavConfig sessionId={sessionId} />
      {children}
    </>
  );
}
