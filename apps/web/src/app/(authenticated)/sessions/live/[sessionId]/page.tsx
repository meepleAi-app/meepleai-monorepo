/**
 * Live Session Main Page — /sessions/live/[sessionId]
 *
 * Game Night Improvvisata — Task 15
 * Phase 5: Game Night — Task 4 (PlayModeMobile on mobile)
 *
 * Desktop: Renders the SessionCardParent component as the primary session overview.
 * Mobile (<640px): Renders PlayModeMobile with 4-tab layout.
 */

'use client';

import { use } from 'react';

import { SessionCardParent } from '@/components/session/live/SessionCardParent';
import { useMediaQuery } from '@/hooks/useMediaQuery';

import { PlayModeMobile } from './play-mode-mobile';

interface LiveSessionPageProps {
  params: Promise<{ sessionId: string }>;
}

export default function LiveSessionPage({ params }: LiveSessionPageProps) {
  const { sessionId } = use(params);
  const isMobile = useMediaQuery('(max-width: 639px)');

  if (isMobile) {
    return <PlayModeMobile sessionId={sessionId} />;
  }

  return <SessionCardParent sessionId={sessionId} />;
}
