/**
 * buildCatanSummaryStandings (#3022) — pure adapter from the raw GameSessionDto score
 * fields + score-aligned players (scorePlayers[].id === scoreData.scores[].playerId) to
 * ordered summary rows enriched with each player's color.
 *
 * mapScoreDataToEndgameSummary maps players.map() preserving order, so the FinalScoreEntry[]
 * is index-parallel to `scorePlayers`, which lets us zip the color by index before sorting.
 */

import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';
import type { ScorePlayerDto } from '@/lib/api/schemas/games.schemas';
import { mapScoreDataToEndgameSummary } from '@/lib/session-live/score-data-to-endgame-summary';

export interface CatanSummaryRow {
  readonly playerName: string;
  readonly score: number;
  readonly isWinner: boolean;
  readonly color: string | null;
}

const SCORE_TYPES: readonly ScoreType[] = ['Points', 'BinaryWin', 'Objectives', 'Ranking'];

export function buildCatanSummaryStandings(
  scoringType: string | null | undefined,
  scoreDataJson: string | null | undefined,
  scorePlayers: readonly ScorePlayerDto[] | null | undefined
): CatanSummaryRow[] {
  if (
    scoringType == null ||
    scoreDataJson == null ||
    scorePlayers == null ||
    scorePlayers.length === 0
  ) {
    return [];
  }
  if (!SCORE_TYPES.includes(scoringType as ScoreType)) return [];

  let parsed: ScoreDataByType[ScoreType];
  try {
    parsed = JSON.parse(scoreDataJson) as ScoreDataByType[ScoreType];
  } catch {
    console.warn(`buildCatanSummaryStandings: malformed scoreData JSON for "${scoringType}"`);
    return [];
  }

  const adapterPlayers = scorePlayers.map(p => ({ id: p.id, name: p.displayName }));
  const entries = mapScoreDataToEndgameSummary(scoringType as ScoreType, parsed, adapterPlayers);
  if (entries.length === 0) return [];

  // entries is index-parallel to scorePlayers (adapter preserves order) → zip color.
  const withColor: CatanSummaryRow[] = entries.map((e, i) => ({
    playerName: e.playerName,
    score: e.score,
    isWinner: e.isWinner,
    color: scorePlayers[i]?.color ?? null,
  }));

  return withColor.sort((a, b) => Number(b.isWinner) - Number(a.isWinner) || b.score - a.score);
}
