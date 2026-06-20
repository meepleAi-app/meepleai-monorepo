/**
 * mapScoreDataToEndgameSummary — pure adapter from polymorphic scoreData
 * (editor / store shape) to EndgameDialog.finalScores entries.
 *
 * Issue #2431 — replaces the hardcoded `isWinner: false` mapping in
 * SessionLiveView.tsx (leftover after #2389 Block B polymorphic migration).
 *
 * Winner rules per variant:
 *   Points     → first player with max points (tie-break by player order).
 *   BinaryWin  → every player whose isWinner flag is true (co-op aware).
 *   Ranking    → position === 1.
 *   Objectives → first player with max completedObjectives.length (tie-break
 *                by player order, no winner if all are zero).
 *
 * Score field semantics (EndgameDialog sorts winners first, then DESC by score):
 *   Points     → raw points.
 *   BinaryWin  → 0 (isWinner alone drives the winner badge; ties on score are
 *                rendered in player order, which matches insertion order).
 *   Ranking    → players.length - position + 1, so the winner has the highest
 *                synthetic score and the last place has 1. Keeps the dialog's
 *                DESC sort natural.
 *   Objectives → completedObjectives.length.
 *
 * Returns `[]` for null gates. Caller decides any legacy fallback.
 *
 * Pure: no side effects, no implicit imports, deterministic.
 */

import type { FinalScoreEntry } from '@/components/features/session-live/EndgameDialog';
import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';

interface AdapterPlayer {
  readonly id: string;
  readonly name: string;
  readonly displayName?: string;
}

export function mapScoreDataToEndgameSummary(
  scoringType: ScoreType | null,
  scoreData: ScoreDataByType[ScoreType] | null,
  players: ReadonlyArray<AdapterPlayer>
): ReadonlyArray<FinalScoreEntry> {
  if (scoringType === null || scoreData === null) return [];

  switch (scoringType) {
    case 'Points': {
      const data = scoreData as ScoreDataByType['Points'];
      const scoresByPlayer = new Map(data.scores.map(s => [s.playerId, s.points]));
      const enriched = players.map(p => ({
        playerName: p.displayName ?? p.name,
        score: scoresByPlayer.get(p.id) ?? 0,
      }));
      const maxScore = enriched.length > 0 ? Math.max(...enriched.map(e => e.score)) : 0;
      const winnerIndex = enriched.findIndex(e => e.score === maxScore);
      return enriched.map((e, i) => ({
        ...e,
        isWinner: i === winnerIndex,
      }));
    }

    case 'BinaryWin': {
      const data = scoreData as ScoreDataByType['BinaryWin'];
      const winnerByPlayer = new Map(data.results.map(r => [r.playerId, r.isWinner]));
      return players.map(p => ({
        playerName: p.displayName ?? p.name,
        score: 0,
        isWinner: winnerByPlayer.get(p.id) ?? false,
      }));
    }

    case 'Ranking': {
      const data = scoreData as ScoreDataByType['Ranking'];
      const positionByPlayer = new Map(data.positions.map(r => [r.playerId, r.position]));
      const lastPosition = players.length;
      return players.map(p => {
        const position = positionByPlayer.get(p.id) ?? lastPosition;
        return {
          playerName: p.displayName ?? p.name,
          score: players.length - position + 1,
          isWinner: position === 1,
        };
      });
    }

    case 'Objectives': {
      const data = scoreData as ScoreDataByType['Objectives'];
      const completedByPlayer = new Map(
        data.completedByPlayer.map(r => [r.playerId, r.objectives.length])
      );
      const enriched = players.map(p => ({
        playerName: p.displayName ?? p.name,
        score: completedByPlayer.get(p.id) ?? 0,
      }));
      const maxScore = enriched.length > 0 ? Math.max(...enriched.map(e => e.score)) : 0;
      // No winner if no one has completed anything (avoids "first player wins by default").
      const winnerIndex = maxScore > 0 ? enriched.findIndex(e => e.score === maxScore) : -1;
      return enriched.map((e, i) => ({
        ...e,
        isWinner: i === winnerIndex,
      }));
    }

    default:
      return assertNever(scoringType);
  }
}

function assertNever(value: never): never {
  throw new Error(`mapScoreDataToEndgameSummary: unhandled scoringType "${value as string}"`);
}
