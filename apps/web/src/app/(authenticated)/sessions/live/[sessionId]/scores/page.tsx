/**
 * Live Session Scores Page — /sessions/live/[sessionId]/scores
 *
 * Game Night Improvvisata — Task 16
 */

'use client';

import { use } from 'react';

import { AutosaveIndicator } from '@/components/session/live/AutosaveIndicator';
import { ScoreBoard } from '@/components/session/live/ScoreBoard';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

interface LiveSessionScoresPageProps {
  params: Promise<{ sessionId: string }>;
}

export default function LiveSessionScoresPage({ params }: LiveSessionScoresPageProps) {
  const { sessionId } = use(params);
  const players = useLiveSessionStore(s => s.players);
  const isHost = players.find(p => p.isHost)?.isHost ?? false;

  return (
    <div className="space-y-2">
      <div className="flex justify-end px-4 pt-2">
        <AutosaveIndicator />
      </div>
      <ScoreBoard sessionId={sessionId} isHost={isHost} />
    </div>
  );
}
