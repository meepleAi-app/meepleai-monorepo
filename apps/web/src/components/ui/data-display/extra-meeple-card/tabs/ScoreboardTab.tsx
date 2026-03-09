'use client';

/**
 * ScoreboardTab - Live scoreboard with player rankings and round scores
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 */

import React from 'react';

import { Crown, Trophy } from 'lucide-react';

import { cn } from '@/lib/utils';

import { PLAYER_COLOR_BG, PLAYER_COLOR_TEXT } from '../../meeple-card-features/session-types';

import type { ScoreboardTabData, SessionPlayerInfo } from '../types';

// ============================================================================
// Sub-components
// ============================================================================

function RankBadge({ rank }: { rank: number }) {
  if (rank === 1) {
    return (
      <div className="flex h-6 w-6 items-center justify-center rounded-full bg-amber-100">
        <Crown className="h-3.5 w-3.5 text-amber-600" />
      </div>
    );
  }
  return (
    <div className="flex h-6 w-6 items-center justify-center rounded-full bg-slate-100 text-xs font-bold text-slate-500">
      {rank}
    </div>
  );
}

function PlayerScoreRow({ player, isLeader }: { player: SessionPlayerInfo; isLeader: boolean }) {
  const bgColor = PLAYER_COLOR_BG[player.color] ?? 'bg-slate-400';
  const textColor = PLAYER_COLOR_TEXT[player.color] ?? 'text-slate-500';

  return (
    <div
      className={cn(
        'flex items-center gap-3 rounded-lg px-3 py-2.5 transition-colors',
        isLeader ? 'bg-amber-50/80 border border-amber-200/50' : 'bg-white/60'
      )}
    >
      <RankBadge rank={player.currentRank} />
      <div
        className={cn(
          'flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold text-white flex-shrink-0',
          bgColor
        )}
      >
        {player.displayName.charAt(0).toUpperCase()}
      </div>
      <div className="min-w-0 flex-1">
        <p className="truncate font-nunito text-sm font-semibold text-slate-800">
          {player.displayName}
        </p>
        {!player.isActive && <p className="text-[10px] text-slate-400 italic">Inactive</p>}
      </div>
      <span className={cn('font-mono text-lg font-bold tabular-nums', textColor)}>
        {player.totalScore}
      </span>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

interface ScoreboardTabProps {
  data?: ScoreboardTabData;
}

export function ScoreboardTab({ data }: ScoreboardTabProps) {
  if (!data || data.players.length === 0) {
    return (
      <div className="flex h-48 flex-col items-center justify-center gap-2 text-slate-400">
        <Trophy className="h-8 w-8 opacity-30" />
        <span className="font-nunito text-sm">No scores yet</span>
      </div>
    );
  }

  // Sort players by rank
  const sortedPlayers = [...data.players].sort((a, b) => a.currentRank - b.currentRank);
  const leaderId = sortedPlayers[0]?.id;

  // Group round scores by round number
  const roundNumbers = [...new Set(data.roundScores.map(s => s.round))].sort((a, b) => a - b);

  return (
    <div className="space-y-4">
      {/* Leaderboard */}
      <div>
        <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-slate-500 font-nunito">
          Leaderboard
        </h3>
        <div className="space-y-1.5">
          {sortedPlayers.map(player => (
            <PlayerScoreRow key={player.id} player={player} isLeader={player.id === leaderId} />
          ))}
        </div>
      </div>

      {/* Round scores table */}
      {roundNumbers.length > 0 && (
        <div className="border-t border-slate-100 pt-3">
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-slate-500 font-nunito">
            Round Scores
          </h3>
          <div className="overflow-x-auto rounded-lg bg-white/50">
            <table className="w-full text-xs font-nunito">
              <thead>
                <tr className="border-b border-slate-100">
                  <th className="px-2 py-1.5 text-left text-slate-400 font-medium">Player</th>
                  {roundNumbers.map(round => (
                    <th key={round} className="px-2 py-1.5 text-center text-slate-400 font-medium">
                      R{round}
                    </th>
                  ))}
                  <th className="px-2 py-1.5 text-right text-slate-400 font-medium">Total</th>
                </tr>
              </thead>
              <tbody>
                {sortedPlayers.map(player => (
                  <tr key={player.id} className="border-b border-slate-50 last:border-0">
                    <td className="px-2 py-1.5 font-medium text-slate-700 truncate max-w-[100px]">
                      {player.displayName}
                    </td>
                    {roundNumbers.map(round => {
                      const score = data.roundScores.find(
                        s => s.playerId === player.id && s.round === round
                      );
                      return (
                        <td
                          key={round}
                          className="px-2 py-1.5 text-center tabular-nums text-slate-600"
                        >
                          {score?.value ?? '–'}
                        </td>
                      );
                    })}
                    <td className="px-2 py-1.5 text-right font-bold tabular-nums text-indigo-600">
                      {player.totalScore}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Scoring config info */}
      {data.scoringConfig && (
        <div className="rounded-lg bg-indigo-50/40 px-3 py-2 text-xs text-indigo-600 font-nunito">
          Scoring: {data.scoringConfig.enabledDimensions.join(', ')}
        </div>
      )}
    </div>
  );
}
