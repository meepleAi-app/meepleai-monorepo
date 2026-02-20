/**
 * SessionTurnSequence - Player Turn Order Chips
 * Issue #4751 - MeepleCard Session Front
 *
 * Displays a horizontal sequence of player chips indicating turn order.
 * Features:
 * - Player color backgrounds
 * - Active player with glow effect + "▶" indicator
 * - Prev/Next arrows for navigation (host only)
 */

'use client';

import React from 'react';

import { ChevronLeft, ChevronRight } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

import { PLAYER_COLOR_MAP, type SessionPlayerInfo, type SessionTurnInfo } from './session-types';

// ============================================================================
// Types
// ============================================================================

export interface SessionTurnSequenceProps {
  players: SessionPlayerInfo[];
  turn: SessionTurnInfo;
  /** Is current user the host (enables controls) */
  isHost?: boolean;
  /** Navigate to previous/next turn */
  onPrevTurn?: () => void;
  onNextTurn?: () => void;
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export const SessionTurnSequence = React.memo(function SessionTurnSequence({
  players,
  turn,
  isHost = false,
  onPrevTurn,
  onNextTurn,
  className,
}: SessionTurnSequenceProps) {
  if (players.length === 0) return null;

  // Sort players by their turn order (use array index as fallback)
  const sortedPlayers = [...players].filter((p) => p.role !== 'spectator');

  return (
    <div
      className={cn('flex items-center gap-1.5', className)}
      data-testid="session-turn-sequence"
    >
      {/* Prev arrow (host only) */}
      {isHost && onPrevTurn && (
        <button
          onClick={(e) => {
            e.stopPropagation();
            onPrevTurn();
          }}
          className="flex-shrink-0 p-0.5 rounded hover:bg-muted transition-colors"
          aria-label="Previous turn"
          data-testid="turn-prev"
        >
          <ChevronLeft className="w-3.5 h-3.5 text-muted-foreground" />
        </button>
      )}

      {/* Player chips */}
      <TooltipProvider delayDuration={200}>
        <div className="flex items-center gap-1 overflow-x-auto">
          {sortedPlayers.map((player, index) => {
            const isActive = turn.currentPlayerId
              ? player.id === turn.currentPlayerId
              : index === turn.currentIndex;
            // eslint-disable-next-line security/detect-object-injection
            const colorHsl = PLAYER_COLOR_MAP[player.color];

            return (
              <Tooltip key={player.id}>
                <TooltipTrigger asChild>
                  <span
                    className={cn(
                      'relative flex items-center justify-center',
                      'min-w-[26px] h-[22px] px-1.5 rounded-full',
                      'text-[9px] font-bold text-white',
                      'transition-all duration-200',
                      isActive && 'ring-2 ring-offset-1 ring-white/60 scale-110',
                    )}
                    style={{
                      backgroundColor: `hsl(${colorHsl})`,
                      ...(isActive
                        ? { boxShadow: `0 0 8px hsla(${colorHsl}, 0.6)` }
                        : {}),
                    }}
                    data-testid={`turn-chip-${player.id}`}
                    aria-current={isActive ? 'step' : undefined}
                  >
                    {isActive && (
                      <span className="mr-0.5" aria-hidden="true">
                        ▶
                      </span>
                    )}
                    {player.displayName.charAt(0).toUpperCase()}
                  </span>
                </TooltipTrigger>
                <TooltipContent side="bottom" sideOffset={4}>
                  <p className="text-xs font-medium">{player.displayName}</p>
                  <p className="text-[10px] text-muted-foreground">
                    Score: {player.totalScore} • Rank #{player.currentRank}
                  </p>
                </TooltipContent>
              </Tooltip>
            );
          })}
        </div>
      </TooltipProvider>

      {/* Next arrow (host only) */}
      {isHost && onNextTurn && (
        <button
          onClick={(e) => {
            e.stopPropagation();
            onNextTurn();
          }}
          className="flex-shrink-0 p-0.5 rounded hover:bg-muted transition-colors"
          aria-label="Next turn"
          data-testid="turn-next"
        >
          <ChevronRight className="w-3.5 h-3.5 text-muted-foreground" />
        </button>
      )}
    </div>
  );
});
