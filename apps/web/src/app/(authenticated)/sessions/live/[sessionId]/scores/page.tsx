/**
 * Live Session Scores Page — /sessions/live/[sessionId]/scores
 *
 * Game Night Improvvisata — Task 16
 */

'use client';

import { use } from 'react';

import { ScoreBoard } from '@/components/sessions/live/ScoreBoard';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

interface LiveSessionScoresPageProps {
  params: Promise<{ sessionId: string }>;
}

export default function LiveSessionScoresPage({ params }: LiveSessionScoresPageProps) {
  const { sessionId } = use(params);
  const players = useLiveSessionStore(s => s.players);
  const isHost = players.find(p => p.isHost)?.isHost ?? false;

  return <ScoreBoard sessionId={sessionId} isHost={isHost} />;
}
