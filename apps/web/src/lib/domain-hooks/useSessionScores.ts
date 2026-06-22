/**
 * useSessionScores Hook
 *
 * Game Night Improvvisata — Task 13 (Block A/C #2389).
 *
 * Surfaces polymorphic scoring config + roster from the live-session store.
 * Returns `scoringType` / `scoreData` verbatim plus a derived `leader`
 * **playerId** (Points-only) for UI highlighting.
 *
 * IMPORTANT — semantic state (#2389 Block C):
 * - The legacy playerName-keyed `scores` map has been removed from the
 *   store (Block A introduced `scoreData`, Block C drops the legacy field).
 * - `leader` is now a `playerId` (not a playerName). Consumers that previously
 *   compared `player.name === leader` must compare `player.id === leader`.
 * - `leader` is `null` whenever `scoringType !== 'Points'`, `scoreData == null`,
 *   or the Points `scores` array is empty.
 */

import { useMemo } from 'react';

import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import type { PlayerInfo, ScoreProposal } from '@/lib/stores/live-session-store';

export interface UseSessionScoresReturn {
  /** Polymorphic scoring discriminator (null until SignalR delivers ScoringConfigured). */
  scoringType: ScoreType | null;
  /** Per-variant scoring payload (mirrors backend `score_data`). */
  scoreData: ScoreDataByType[ScoreType] | null;
  players: PlayerInfo[];
  pendingProposals: ScoreProposal[];
  /**
   * Current leader's `playerId`, derived from `scoreData.scores` when
   * `scoringType === 'Points'`. Returns `null` for every other variant
   * (BinaryWin, Objectives, Ranking) and when no scores have been recorded yet.
   */
  leader: string | null;
}

/**
 * Reads scoringType, scoreData, players and pending proposals from the
 * live-session-store. Derives the leader `playerId` (Points-only) via a
 * memoised selector.
 *
 * @param _sessionId - Reserved for future per-session store isolation; unused today.
 */
export function useSessionScores(_sessionId?: string): UseSessionScoresReturn {
  const scoringType = useLiveSessionStore(s => s.scoringType);
  const scoreData = useLiveSessionStore(s => s.scoreData);
  const players = useLiveSessionStore(s => s.players);
  const pendingProposals = useLiveSessionStore(s => s.pendingProposals);

  const leader = useMemo<string | null>(() => {
    if (scoringType !== 'Points' || scoreData == null) return null;
    const pointsData = scoreData as ScoreDataByType['Points'];
    if (!pointsData.scores.length) return null;
    return pointsData.scores.reduce((best, current) =>
      current.points > best.points ? current : best
    ).playerId;
  }, [scoringType, scoreData]);

  return { scoringType, scoreData, players, pendingProposals, leader };
}
