/**
 * useSessionScores Hook
 *
 * Game Night Improvvisata — Task 13 (Block A #2389 extension).
 *
 * Surfaces polymorphic scoring config + roster from the live-session store.
 * Returns the new `scoringType` / `scoreData` fields verbatim, plus a derived
 * legacy `scores` map (Points-only — keyed by `playerId`, not playerName).
 *
 * IMPORTANT — semantic shift (#2389 Block A):
 * - The derived `scores` returned by this hook is keyed by `playerId`
 *   (e.g. `{ p1: 12, p2: 7 }`), unlike the legacy store field
 *   `useLiveSessionStore.s.scores` which is keyed by **playerName**
 *   (populated via `updateScore(playerName, score)`).
 * - The `leader` field is intentionally still derived from the legacy
 *   playerName-keyed store map so existing consumers (e.g. `ScoreBoard.tsx`,
 *   which compares `player.name === leader`) keep working until Task 9+
 *   migrates them. Consumers expecting `leader` to be a `playerId` should
 *   not rely on this hook yet.
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
  /**
   * Derived per-player score map.
   *
   * @deprecated #2389 Block A — derived from `scoreData` when scoringType
   * is 'Points'; empty `{}` for every other variant. **Keyed by `playerId`**
   * (semantic shift from the legacy playerName-keyed store field). Migrate
   * consumers to read `scoreData` directly.
   */
  scores: Record<string, number>;
  players: PlayerInfo[];
  pendingProposals: ScoreProposal[];
  /**
   * Current leader (playerName).
   *
   * Computed from the legacy store `scores` field (playerName-keyed) so
   * existing consumers comparing `player.name === leader` keep working.
   * Returns null when no scores exist.
   */
  leader: string | null;
}

/**
 * Reads scoringType, scoreData, players and pending proposals from the
 * live-session-store. Derives a legacy `scores` map (Points variant only,
 * playerId-keyed) and the leader playerName via memoised selectors.
 *
 * @param _sessionId - Reserved for future per-session store isolation; unused today.
 */
export function useSessionScores(_sessionId?: string): UseSessionScoresReturn {
  const scoringType = useLiveSessionStore(s => s.scoringType);
  const scoreData = useLiveSessionStore(s => s.scoreData);
  const players = useLiveSessionStore(s => s.players);
  const pendingProposals = useLiveSessionStore(s => s.pendingProposals);
  const legacyScores = useLiveSessionStore(s => s.scores);

  const scores = useMemo<Record<string, number>>(() => {
    if (scoringType !== 'Points' || scoreData == null) return {};
    const pointsData = scoreData as ScoreDataByType['Points'];
    return Object.fromEntries(pointsData.scores.map(s => [s.playerId, s.points]));
  }, [scoringType, scoreData]);

  const leader = useMemo<string | null>(() => {
    const entries = Object.entries(legacyScores);
    if (!entries.length) return null;
    return entries.reduce((best, current) => (current[1] > best[1] ? current : best))[0];
  }, [legacyScores]);

  return { scoringType, scoreData, scores, players, pendingProposals, leader };
}
