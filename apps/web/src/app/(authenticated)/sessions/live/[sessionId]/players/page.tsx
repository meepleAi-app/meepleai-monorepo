/**
 * Players Child Card — /sessions/live/[sessionId]/players
 *
 * Game Night Improvvisata — Task 21
 *
 * Shows the session player list with online indicators, host crown,
 * and invite modal.
 */

'use client';

import { use } from 'react';

import { PlayerList } from '@/components/sessions/live/PlayerList';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

interface PlayersPageProps {
  params: Promise<{ sessionId: string }>;
}

export default function PlayersPage({ params }: PlayersPageProps) {
  const { sessionId } = use(params);

  // Read invite info from store (set when session starts via startImprovvisata)
  // The store doesn't hold inviteCode/shareLink directly; we derive the share link
  // from the current URL pattern. The inviteCode is read from sessionInfo if available.
  const gameName = useLiveSessionStore(s => s.gameName);

  // Build a fallback share link from the current session ID
  const shareLink =
    typeof window !== 'undefined'
      ? `${window.location.origin}/join/${sessionId}`
      : `/join/${sessionId}`;

  return (
    <PlayerList
      sessionId={sessionId}
      inviteCode={sessionId.slice(0, 6).toUpperCase()}
      shareLink={shareLink}
    />
  );
}
