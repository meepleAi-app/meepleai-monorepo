/**
 * Round-based scoring of a LiveGameSession — #4156 fetta 2, ADR-089.
 *
 * The live page scores on `RoundScores`: one cell per player × round × dimension, with the
 * dimensions the wizard configured (`ScoringConfig`). The backend replaces a cell on every write
 * and recalculates each player's `totalScore`, which is the only total this module trusts.
 */

import type { LiveSessionDto, LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

/** Who scores: the active players the turn rotates among. Spectators score nothing. */
export function scoringPlayers(session: LiveSessionDto): LiveSessionPlayerDto[] {
  return session.players.filter(player => player.isActive && player.role !== 'Spectator');
}

/**
 * The round of the game, derived from the turn index (#4156 D6; session-assistant spec §5.1).
 *
 * `CurrentTurnIndex` is 0 before the start, 1 at `Start()`, and grows at every turn of every
 * player; a round is a full lap of the table among `playerCount` scoring players. Before the start,
 * or without players, the round is 1.
 */
export function deriveRound(currentTurnIndex: number, playerCount: number): number {
  if (currentTurnIndex < 1 || playerCount < 1) return 1;
  return Math.floor((currentTurnIndex - 1) / playerCount) + 1;
}

/** The value of a cell, or `undefined` when nobody has scored it. Dimensions ignore case. */
export function cellValue(
  session: LiveSessionDto,
  playerId: string,
  round: number,
  dimension: string
): number | undefined {
  const wanted = dimension.toLowerCase();
  return session.roundScores.find(
    score =>
      score.playerId === playerId &&
      score.round === round &&
      score.dimension.toLowerCase() === wanted
  )?.value;
}

/** Scores are written only while the game is running, as the domain requires. */
export function canRecordScores(session: LiveSessionDto): boolean {
  return session.status === 'InProgress' || session.status === 'Paused';
}

export interface FinalScore {
  readonly playerName: string;
  readonly score: number;
  readonly isWinner: boolean;
}

/**
 * The final scores shown before closing the game: each scoring player with the total the backend
 * recalculated. The winner is the player alone in the lead; on a tie for the lead there is none.
 */
export function finalScores(session: LiveSessionDto): FinalScore[] {
  const players = scoringPlayers(session);
  const best = Math.max(...players.map(player => player.totalScore));
  const leaders = players.filter(player => player.totalScore === best);
  return players.map(player => ({
    playerName: player.displayName,
    score: player.totalScore,
    isWinner: leaders.length === 1 && leaders[0] === player,
  }));
}
