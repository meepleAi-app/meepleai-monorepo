/**
 * SessionScoreTable - Player x Round Score Matrix
 * Issue #4751 - MeepleCard Session Front
 *
 * Displays a compact score matrix with player rows and round columns.
 * Features:
 * - Player dots with assigned colors
 * - Leader row highlighted with crown emoji
 * - "Modifica Punteggio" button to open score editor
 * - Responsive: truncates rounds on small cards
 */

'use client';

import React from 'react';

import { Crown, Pencil } from 'lucide-react';

import { cn } from '@/lib/utils';

import { PLAYER_COLOR_BG, type SessionPlayerInfo, type SessionRoundScore } from './session-types';

// ============================================================================
// Types
// ============================================================================

export interface SessionScoreTableProps {
  players: SessionPlayerInfo[];
  roundScores: SessionRoundScore[];
  /** Max rounds to display before truncating (default: 5) */
  maxVisibleRounds?: number;
  /** Callback for edit button */
  onEditScore?: () => void;
  /** Active dimension tab (for multi-dimension scoring) */
  dimension?: string;
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

function buildScoreMatrix(
  players: SessionPlayerInfo[],
  roundScores: SessionRoundScore[],
  dimension: string
): { playerId: string; scores: Record<number, number>; total: number }[] {
  return players.map(p => {
    const playerScores = roundScores.filter(s => s.playerId === p.id && s.dimension === dimension);
    const scores: Record<number, number> = {};
    for (const s of playerScores) {
      scores[s.round] = s.value;
    }
    return { playerId: p.id, scores, total: p.totalScore };
  });
}

// ============================================================================
// Component
// ============================================================================

export const SessionScoreTable = React.memo(function SessionScoreTable({
  players,
  roundScores,
  maxVisibleRounds = 5,
  onEditScore,
  dimension = 'default',
  className,
}: SessionScoreTableProps) {
  if (players.length === 0) return null;

  const matrix = buildScoreMatrix(players, roundScores, dimension);
  const allRounds = [
    ...new Set(roundScores.filter(s => s.dimension === dimension).map(s => s.round)),
  ].sort((a, b) => a - b);
  const visibleRounds = allRounds.slice(0, maxVisibleRounds);
  const hasMore = allRounds.length > maxVisibleRounds;
  const leaderId = players.reduce(
    (best, p) => (p.totalScore > (best?.totalScore ?? -Infinity) ? p : best),
    players[0]
  )?.id;

  return (
    <div className={cn('flex flex-col gap-1.5', className)} data-testid="session-score-table">
      {/* Score matrix */}
      <div className="overflow-x-auto">
        <table className="w-full text-[10px]">
          <thead>
            <tr className="text-muted-foreground/70">
              <th className="text-left font-medium pr-2 pb-1">Player</th>
              {visibleRounds.map(r => (
                <th key={r} className="text-center font-medium px-1 pb-1 min-w-[24px]">
                  R{r}
                </th>
              ))}
              {hasMore && <th className="text-center font-medium px-1 pb-1">...</th>}
              <th className="text-right font-bold px-1 pb-1">Tot</th>
            </tr>
          </thead>
          <tbody>
            {players.map(player => {
              const row = matrix.find(m => m.playerId === player.id);
              const isLeader = player.id === leaderId;
              return (
                <tr
                  key={player.id}
                  className={cn(
                    'transition-colors',
                    isLeader && 'bg-amber-50/60 dark:bg-amber-900/10'
                  )}
                  data-testid={`score-row-${player.id}`}
                >
                  <td className="pr-2 py-0.5">
                    <span className="flex items-center gap-1.5 truncate max-w-[80px]">
                      <span
                        className={cn(
                          'w-2.5 h-2.5 rounded-full flex-shrink-0',

                          PLAYER_COLOR_BG[player.color]
                        )}
                        aria-hidden="true"
                      />
                      <span className="truncate font-medium">{player.displayName}</span>
                      {isLeader && (
                        <Crown
                          className="w-3 h-3 text-amber-500 flex-shrink-0"
                          aria-label="Leader"
                        />
                      )}
                    </span>
                  </td>
                  {visibleRounds.map(r => (
                    <td key={r} className="text-center px-1 py-0.5 tabular-nums">
                      {/* eslint-disable-next-line security/detect-object-injection */}
                      {row?.scores[r] ?? '-'}
                    </td>
                  ))}
                  {hasMore && (
                    <td className="text-center px-1 py-0.5 text-muted-foreground">...</td>
                  )}
                  <td className="text-right px-1 py-0.5 font-bold tabular-nums">
                    {row?.total ?? 0}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Edit button */}
      {onEditScore && (
        <button
          onClick={e => {
            e.stopPropagation();
            onEditScore();
          }}
          className={cn(
            'flex items-center justify-center gap-1.5',
            'w-full py-1.5 rounded-md',
            'text-[10px] font-semibold',
            'bg-indigo-50 text-indigo-600 dark:bg-indigo-900/20 dark:text-indigo-300',
            'hover:bg-indigo-100 dark:hover:bg-indigo-900/30',
            'transition-colors duration-200'
          )}
          data-testid="edit-score-button"
        >
          <Pencil className="w-3 h-3" />
          Modifica Punteggio
        </button>
      )}
    </div>
  );
});
