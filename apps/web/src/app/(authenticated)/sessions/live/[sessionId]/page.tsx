/**
 * Live Session Main Page — /sessions/live/[sessionId]
 *
 * Game Night Improvvisata — Task 15
 * Phase 5: Game Night — Task 4 (PlayModeMobile on mobile)
 * Game Session Flow v2.0 — Task 10 (GameOverModal for desktop)
 *
 * Desktop: Renders the SessionCardParent component as the primary session overview.
 *          Shows GameOverModal when session status is 'Completed'.
 * Mobile (<lg): Renders PlayModeMobile with 4-tab layout (handles its own GameOverModal).
 */

'use client';

import { use } from 'react';

import { GameOverModal } from '@/components/session/live/GameOverModal';
import type { GameOverPlayer } from '@/components/session/live/GameOverModal';
import { SessionCardParent } from '@/components/session/live/SessionCardParent';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

const AVATAR_COLORS = [
  '#ef4444',
  '#3b82f6',
  '#22c55e',
  '#eab308',
  '#a855f7',
  '#f97316',
  '#ec4899',
  '#14b8a6',
];

import { PlayModeMobile } from './play-mode-mobile';

interface LiveSessionPageProps {
  params: Promise<{ sessionId: string }>;
}

export default function LiveSessionPage({ params }: LiveSessionPageProps) {
  const { sessionId } = use(params);

  // Desktop game-over state
  const status = useLiveSessionStore(s => s.status);
  const gameName = useLiveSessionStore(s => s.gameName);
  const players = useLiveSessionStore(s => s.players);
  const scores = useLiveSessionStore(s => s.scores);

  const gameOverPlayers: GameOverPlayer[] = [...players]
    .map((p, i) => ({
      id: p.id,
      displayName: p.name,
      totalScore: scores[p.name] ?? 0,
      rank: 0,
      avatarColor: AVATAR_COLORS[i % AVATAR_COLORS.length] ?? '#6b7280',
    }))
    .sort((a, b) => b.totalScore - a.totalScore)
    .map((p, i) => ({ ...p, rank: i + 1 }));

  return (
    <>
      <div className="lg:hidden">
        <PlayModeMobile sessionId={sessionId} />
      </div>
      <div className="hidden lg:block">
        {status === 'Completed' ? (
          <GameOverModal
            gameName={gameName || 'Sessione'}
            players={gameOverPlayers}
            sessionId={sessionId}
          />
        ) : (
          <SessionCardParent sessionId={sessionId} />
        )}
      </div>
    </>
  );
}
