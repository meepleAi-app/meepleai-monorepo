/**
 * useSessionScores Hook
 *
 * Game Night Improvvisata — Task 13
 *
 * Derives score-related data from the live-session-store.
 * Memoises the leader calculation to avoid unnecessary re-renders.
 */

import { useMemo } from 'react';

import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import type { PlayerInfo, ScoreProposal } from '@/lib/stores/live-session-store';

export interface UseSessionScoresReturn {
  /** Current cumulative scores, keyed by player name */
  scores: Record<string, number>;
  /** Player info array (order, host flag, online status) */
  players: PlayerInfo[];
  /** Score proposals awaiting host confirmation */
  pendingProposals: ScoreProposal[];
  /** Player name currently leading (null when no scores exist) */
  leader: string | null;
}

/**
 * useSessionScores
 *
 * Reads scores, players and pending proposals from the live-session-store.
 * Computes the current leader via a memoised selector.
 *
 * @param _sessionId - Reserved for future per-session store isolation; unused today.
 */
export function useSessionScores(_sessionId?: string): UseSessionScoresReturn {
  const scores = useLiveSessionStore(s => s.scores);
  const players = useLiveSessionStore(s => s.players);
  const pendingProposals = useLiveSessionStore(s => s.pendingProposals);

  const leader = useMemo<string | null>(() => {
    const entries = Object.entries(scores);
    if (!entries.length) return null;
    return entries.reduce((best, current) => (current[1] > best[1] ? current : best))[0];
  }, [scores]);

  return { scores, players, pendingProposals, leader };
}
