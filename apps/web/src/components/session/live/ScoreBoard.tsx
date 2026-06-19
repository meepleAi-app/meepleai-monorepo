/**
 * ScoreBoard
 *
 * Game Night Improvvisata — Task 16 (Block C #2389 cleanup).
 *
 * Read-only scoreboard surfaced to non-Host viewers (spectators, guests).
 * The Host edit path moved into `ScoreTabContent` (`PolymorphicScoreEditor` +
 * REST autosave, Block B+ #2430), so this component intentionally has no
 * mutation surface and no proposal handling.
 *
 * Scores are looked up by `playerId` (Block C migration). For `scoringType`
 * variants other than `'Points'` every player renders as `0` because the
 * polymorphic payload (`BinaryWin` / `Objectives` / `Ranking`) is rendered
 * elsewhere via the strategy primitives.
 */

'use client';

import { useMemo } from 'react';

import { Crown } from 'lucide-react';

import type { ScoreDataByType } from '@/components/sessions/score-strategies/types';
import { useSessionScores } from '@/lib/domain-hooks/useSessionScores';
import type { PlayerInfo } from '@/lib/stores/live-session-store';

// ─── PlayerScoreCard ──────────────────────────────────────────────────────────

interface PlayerScoreCardProps {
  player: PlayerInfo;
  score: number;
  isLeader: boolean;
}

function PlayerScoreCard({ player, score, isLeader }: PlayerScoreCardProps) {
  return (
    <div
      className={[
        'rounded-xl border p-3 space-y-2 transition-shadow',
        isLeader
          ? 'bg-amber-50 border-amber-300 shadow-md shadow-amber-100'
          : 'bg-card/70 backdrop-blur-md border-border shadow-sm',
        player.isOnline ? '' : 'opacity-60',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      {/* Player name + leader badge */}
      <div className="flex items-center justify-between gap-1">
        <span className="font-quicksand font-semibold text-sm truncate text-foreground">
          {player.name}
        </span>
        {isLeader && <Crown className="h-4 w-4 shrink-0 text-amber-500" aria-label="Leader" />}
      </div>

      {/* Score */}
      <p
        data-testid={`player-score-${player.id}`}
        className="text-2xl font-quicksand font-bold text-foreground"
      >
        {score}
      </p>

      {/* Online indicator */}
      <div className="flex items-center gap-1">
        <span
          className={['h-2 w-2 rounded-full', player.isOnline ? 'bg-green-400' : 'bg-muted'].join(
            ' '
          )}
        />
        <span className="text-xs text-muted-foreground font-nunito">
          {player.isOnline ? 'Online' : 'Offline'}
        </span>
      </div>
    </div>
  );
}

// ─── ScoreBoard ────────────────────────────────────────────────────────────────

interface ScoreBoardProps {
  sessionId: string;
}

export function ScoreBoard({ sessionId }: ScoreBoardProps) {
  const { players, scoringType, scoreData, leader } = useSessionScores(sessionId);

  /**
   * Derive a `playerId → points` map locally from `scoreData` when the
   * scoring variant is `'Points'`. Returns an empty map for any other
   * scoring type (BinaryWin / Objectives / Ranking) — those variants are
   * rendered by their dedicated strategy primitives, not by this scoreboard.
   */
  const scoresMap = useMemo<Record<string, number>>(() => {
    if (scoringType !== 'Points' || scoreData == null) return {};
    const pointsData = scoreData as ScoreDataByType['Points'];
    return Object.fromEntries(pointsData.scores.map(s => [s.playerId, s.points]));
  }, [scoringType, scoreData]);

  return (
    <div className="space-y-6 p-4">
      <h2 className="text-xl font-quicksand font-bold text-foreground">Punteggi</h2>

      {/* Empty state */}
      {players.length === 0 && (
        <p className="text-sm text-muted-foreground font-nunito text-center py-8">
          Nessun giocatore ancora registrato.
        </p>
      )}

      {/* Player score cards */}
      {players.length > 0 && (
        <div className="grid grid-cols-2 gap-3">
          {players.map(player => (
            <PlayerScoreCard
              key={player.id}
              player={player}
              score={scoresMap[player.id] ?? 0}
              isLeader={player.id === leader}
            />
          ))}
        </div>
      )}
    </div>
  );
}
