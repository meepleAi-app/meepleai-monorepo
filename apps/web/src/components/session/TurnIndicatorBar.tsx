'use client';

/**
 * TurnIndicatorBar — sticky sub-header showing the active player and round
 * Epic #4968: Game Session Toolkit v2
 */

import React from 'react';

import { ChevronRight } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

// ── Component ─────────────────────────────────────────────────────────────────

interface TurnIndicatorBarProps {
  activePlayerName: string;
  roundNumber: number;
  canEndTurn: boolean;
  onEndTurn: () => void;
}

export function TurnIndicatorBar({
  activePlayerName,
  roundNumber,
  canEndTurn,
  onEndTurn,
}: TurnIndicatorBarProps) {
  return (
    <div className="flex items-center justify-between px-4 py-2 bg-amber-50 dark:bg-amber-950/30 border-b border-amber-200/50 dark:border-amber-800/30">
      <div className="flex items-center gap-3">
        <span className="text-xs text-slate-500 dark:text-slate-400 font-semibold uppercase tracking-wide">
          Round {roundNumber}
        </span>
        <span className="text-slate-300 dark:text-slate-600" aria-hidden="true">
          ·
        </span>
        <span className="flex items-center gap-1.5 text-sm font-semibold text-amber-700 dark:text-amber-300">
          <span
            className="inline-block w-2 h-2 rounded-full bg-emerald-500 animate-pulse"
            aria-hidden="true"
          />
          <span>
            <span className="sr-only">Active player: </span>
            {activePlayerName}&apos;s turn
          </span>
        </span>
      </div>

      {canEndTurn && (
        <Button
          size="sm"
          variant="outline"
          onClick={onEndTurn}
          className="flex items-center gap-1 text-xs border-amber-300 text-amber-700 dark:border-amber-700 dark:text-amber-300 hover:bg-amber-100 dark:hover:bg-amber-900/30"
        >
          End Turn
          <ChevronRight className="w-3.5 h-3.5" aria-hidden="true" />
        </Button>
      )}
    </div>
  );
}
