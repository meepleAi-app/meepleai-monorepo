/**
 * TurnStateHeader — shows current turn, phase, and active player
 *
 * Game Session Flow v2.0 — Task 12
 *
 * Displays:
 * - Current turn number
 * - Current phase name (if configured)
 * - Active player name
 * - Advance turn / advance phase buttons
 */

'use client';

import { ChevronRight, Layers } from 'lucide-react';

import { cn } from '@/lib/utils';

interface TurnStateHeaderProps {
  currentTurn: number;
  currentPhase: string | null;
  phaseCount: number;
  currentPhaseIndex: number;
  activePlayerName: string | null;
  canAdvanceTurn: boolean;
  canAdvancePhase: boolean;
  onAdvanceTurn: () => void;
  onAdvancePhase: () => void;
  className?: string;
}

export function TurnStateHeader({
  currentTurn,
  currentPhase,
  phaseCount,
  currentPhaseIndex,
  activePlayerName,
  canAdvanceTurn,
  canAdvancePhase,
  onAdvanceTurn,
  onAdvancePhase,
  className,
}: TurnStateHeaderProps) {
  return (
    <div
      className={cn(
        'flex items-center gap-3 px-4 py-2.5 bg-white/5 border-b border-white/10',
        className
      )}
    >
      {/* Turn number */}
      <div className="flex flex-col items-center shrink-0">
        <span className="text-[10px] text-muted-foreground uppercase tracking-wider">Turno</span>
        <span className="text-lg font-bold font-mono tabular-nums leading-tight">
          {currentTurn}
        </span>
      </div>

      <div className="w-px h-8 bg-white/10 shrink-0" />

      {/* Phase info */}
      {phaseCount > 0 && (
        <>
          <div className="flex items-center gap-1.5 shrink-0">
            <Layers className="h-3.5 w-3.5 text-purple-400" />
            <div className="flex flex-col">
              <span className="text-[10px] text-muted-foreground uppercase tracking-wider">
                Fase {currentPhaseIndex + 1}/{phaseCount}
              </span>
              <span className="text-sm font-semibold leading-tight">
                {currentPhase ?? `Fase ${currentPhaseIndex + 1}`}
              </span>
            </div>
          </div>
          <div className="w-px h-8 bg-white/10 shrink-0" />
        </>
      )}

      {/* Active player */}
      {activePlayerName && (
        <div className="flex-1 min-w-0">
          <span className="text-[10px] text-muted-foreground uppercase tracking-wider block">
            Gioca
          </span>
          <span className="text-sm font-semibold truncate block">{activePlayerName}</span>
        </div>
      )}

      {!activePlayerName && <div className="flex-1" />}

      {/* Action buttons */}
      <div className="flex gap-1.5 shrink-0">
        {canAdvancePhase && phaseCount > 0 && (
          <button
            onClick={onAdvancePhase}
            title="Avanza fase"
            className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-xs font-medium bg-purple-500/20 text-purple-300 hover:bg-purple-500/30 transition-colors border border-purple-500/30"
          >
            <Layers className="h-3.5 w-3.5" />
            Fase
          </button>
        )}
        {canAdvanceTurn && (
          <button
            onClick={onAdvanceTurn}
            title="Avanza turno"
            className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-xs font-medium bg-amber-500/20 text-amber-300 hover:bg-amber-500/30 transition-colors border border-amber-500/30"
          >
            <ChevronRight className="h-3.5 w-3.5" />
            Turno
          </button>
        )}
      </div>
    </div>
  );
}
