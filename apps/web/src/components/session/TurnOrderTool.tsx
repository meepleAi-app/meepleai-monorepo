'use client';

/**
 * TurnOrderTool — shows the current turn order and lets the host advance or reset
 * Epic #4968: Game Session Toolkit v2
 */

import React, { useState } from 'react';

import { ChevronRight, RotateCcw, User } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

import type { TurnOrderData } from './types';

// ── Component ─────────────────────────────────────────────────────────────────

interface TurnOrderToolProps {
  turnOrder: TurnOrderData;
  /** Only hosts can advance / reset the turn */
  isHost: boolean;
  onAdvanceTurn: () => Promise<void>;
  onResetTurnOrder: () => Promise<void>;
}

export function TurnOrderTool({
  turnOrder,
  isHost,
  onAdvanceTurn,
  onResetTurnOrder,
}: TurnOrderToolProps) {
  const [advancing, setAdvancing] = useState(false);
  const [resetting, setResetting] = useState(false);

  const handleAdvance = async () => {
    setAdvancing(true);
    try {
      await onAdvanceTurn();
    } finally {
      setAdvancing(false);
    }
  };

  const handleReset = async () => {
    setResetting(true);
    try {
      await onResetTurnOrder();
    } finally {
      setResetting(false);
    }
  };

  const nextIndex = (turnOrder.currentIndex + 1) % turnOrder.playerOrder.length;

  return (
    <div className="max-w-md mx-auto space-y-6">
      {/* Round badge */}
      <div className="text-center">
        <span className="inline-flex items-center px-4 py-1.5 rounded-full text-sm font-bold bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300 ring-1 ring-amber-300 dark:ring-amber-700">
          Round {turnOrder.roundNumber}
        </span>
      </div>

      {/* Player order list */}
      <ol aria-label="Turn order" className="space-y-2">
        {turnOrder.playerOrder.map((player, idx) => {
          const isCurrent = idx === turnOrder.currentIndex;
          const isNext = idx === nextIndex && !isCurrent;

          return (
            <li
              key={player}
              className={`flex items-center gap-3 rounded-xl px-4 py-3 transition-all ${
                isCurrent
                  ? 'bg-amber-100 dark:bg-amber-900/40 ring-2 ring-amber-400 dark:ring-amber-600'
                  : 'bg-white dark:bg-slate-800/60 ring-1 ring-slate-200 dark:ring-slate-700'
              }`}
            >
              <span className="flex h-7 w-7 items-center justify-center rounded-full bg-slate-100 dark:bg-slate-700 text-xs font-bold text-slate-600 dark:text-slate-300 shrink-0">
                {idx + 1}
              </span>

              <User
                className="w-4 h-4 text-slate-400 dark:text-slate-500 shrink-0"
                aria-hidden="true"
              />

              <span
                className={`flex-1 font-medium ${
                  isCurrent
                    ? 'text-amber-800 dark:text-amber-200'
                    : 'text-slate-700 dark:text-slate-300'
                }`}
              >
                {player}
              </span>

              {isCurrent && (
                <span className="text-[10px] font-semibold uppercase tracking-wide px-2 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300">
                  Playing
                </span>
              )}
              {isNext && (
                <span className="text-[10px] font-semibold uppercase tracking-wide px-2 py-0.5 rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300">
                  Next
                </span>
              )}
            </li>
          );
        })}
      </ol>

      {/* Host actions */}
      {isHost && (
        <div className="flex items-center gap-3">
          <Button
            onClick={() => void handleAdvance()}
            disabled={advancing}
            className="flex-1 flex items-center justify-center gap-2 bg-amber-600 hover:bg-amber-700 text-white"
          >
            <ChevronRight className="w-4 h-4" aria-hidden="true" />
            {advancing ? 'Advancing…' : 'Advance Turn'}
          </Button>

          <Button
            variant="outline"
            onClick={() => void handleReset()}
            disabled={resetting}
            className="flex items-center gap-1.5 text-slate-600 dark:text-slate-400"
          >
            <RotateCcw className="w-4 h-4" aria-hidden="true" />
            {resetting ? 'Resetting…' : 'Reset'}
          </Button>
        </div>
      )}
    </div>
  );
}
