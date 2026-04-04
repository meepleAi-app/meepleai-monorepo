/**
 * Adapter functions: LiveSessionDto → legacy component types
 *
 * Bridges the new LiveSession API schemas to the existing Session Toolkit
 * component interfaces (Session, Participant, ScoreEntry, ScoreboardData).
 *
 * Issue #5041 — Sessions Redesign Phase 2
 */

import type {
  LiveSessionDto,
  LiveSessionPlayerDto,
  LiveSessionRoundScoreDto,
  PlayerColor,
} from '@/lib/api/schemas/live-sessions.schemas';

import type { Participant, ScoreEntry, ScoreboardData, Session } from './types';

const PLAYER_COLOR_HEX: Record<PlayerColor, string> = {
  Red: '#ef4444',
  Blue: '#3b82f6',
  Green: '#22c55e',
  Yellow: '#eab308',
  Purple: '#a855f7',
  Orange: '#f97316',
  White: '#f5f5f5',
  Black: '#1f2937',
  Pink: '#ec4899',
  Teal: '#14b8a6',
};

/** Maps a PlayerColor enum value to its hex code. */
export function playerColorToHex(color: PlayerColor): string {
  return PLAYER_COLOR_HEX[color] ?? '#6b7280';
}

/** Maps a LiveSessionDto to the legacy Session interface used by SessionHeader. */
export function toSession(dto: LiveSessionDto): Session {
  const statusMap: Record<string, Session['status']> = {
    Created: 'Active',
    Setup: 'Active',
    InProgress: 'Active',
    Paused: 'Paused',
    Completed: 'Finalized',
  };

  return {
    id: dto.id,
    sessionCode: dto.sessionCode,
    sessionType: dto.gameId ? 'GameSpecific' : 'Generic',
    gameId: dto.gameId,
    gameName: dto.gameName || undefined,
    sessionDate: new Date(dto.createdAt),
    status: statusMap[dto.status] ?? 'Active',
    participantCount: dto.players.length,
  };
}

/** Maps a LiveSessionPlayerDto to the legacy Participant interface. */
export function toParticipant(
  player: LiveSessionPlayerDto,
  currentUserId: string | null
): Participant {
  return {
    id: player.id,
    displayName: player.displayName,
    isOwner: player.role === 'Host',
    isCurrentUser: currentUserId !== null && player.userId === currentUserId,
    avatarColor: playerColorToHex(player.color),
    totalScore: player.totalScore,
    rank: player.currentRank > 0 ? player.currentRank : undefined,
  };
}

/** Maps a LiveSessionRoundScoreDto to the legacy ScoreEntry interface. */
export function toScoreEntry(score: LiveSessionRoundScoreDto): ScoreEntry {
  return {
    id: `${score.playerId}-r${score.round}-${score.dimension}`,
    participantId: score.playerId,
    roundNumber: score.round,
    category: score.dimension === 'default' ? null : score.dimension,
    scoreValue: score.value,
    timestamp: new Date(score.recordedAt),
    createdBy: score.playerId,
  };
}

/** Builds a ScoreboardData object from a LiveSessionDto and its scores. */
export function toScoreboardData(
  dto: LiveSessionDto,
  scores: LiveSessionRoundScoreDto[],
  currentUserId: string | null
): ScoreboardData {
  const participants = dto.players.map(p => toParticipant(p, currentUserId));
  const scoreEntries = scores.map(toScoreEntry);

  const roundSet = new Set<number>();
  const categorySet = new Set<string>();

  for (const s of scores) {
    roundSet.add(s.round);
    if (s.dimension !== 'default') {
      categorySet.add(s.dimension);
    }
  }

  const rounds = [...roundSet].sort((a, b) => a - b);
  const categories = [...categorySet].sort();

  return { participants, scores: scoreEntries, rounds, categories };
}
