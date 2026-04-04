/**
 * Scoreboard Route — /sessions/{id}/scoreboard
 *
 * Dedicated full-page scoreboard view for a game session.
 * Fetches session via api.sessions.getById and renders ranked player list.
 *
 * Task 3 — Sessions Redesign
 */

import { use } from 'react';

import { ScoreboardPage } from '@/components/session/ScoreboardPage';

interface ScoreboardRouteProps {
  params: Promise<{ id: string }>;
}

export default function ScoreboardRoute({ params }: ScoreboardRouteProps) {
  const { id } = use(params);
  return <ScoreboardPage sessionId={id} />;
}
