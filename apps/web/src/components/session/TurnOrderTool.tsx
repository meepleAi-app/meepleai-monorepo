'use client';

/**
 * TurnOrderTool — Full turn order panel (Issue #4975).
 *
 * Shows the ordered list of players, highlights the current player,
 * and provides host-only turn control actions.
 *
 * Wired to useTurnOrder hook for state management and API calls.
 * Real-time updates applied via applySSEAdvance from the hook.
 */

import React, { useState } from 'react';

import { Loader2, RotateCcw, ChevronRight, RefreshCw } from 'lucide-react';

import type { TurnOrderData } from '@/components/session/types';
import { cn } from '@/lib/utils';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface TurnOrderToolProps {
  /** Current turn order data (null when not yet initialized). */
  turnOrder: TurnOrderData | null;
  /** Whether data is loading. */
  isLoading?: boolean;
  /** Error message from the hook. */
  error?: string | null;
  /** Whether the current user is the session host. */
  isHost?: boolean;
  /** Advance to the next player's turn. Host-only. */
  onAdvanceTurn?: () => Promise<void>;
  /** Reset turn order to round 1, player 1. Host-only. */
  onResetTurnOrder?: () => Promise<void>;
  className?: string;
}

// ── Component ─────────────────────────────────────────────────────────────────

/**
 * TurnOrderTool
 *
 * Full-panel turn order view for the session toolkit.
 * Rendered when `activeTool === 'turn-order'` in the session page.
 */
export function TurnOrderTool({
  turnOrder,
  isLoading = false,
  error = null,
  isHost = false,
  onAdvanceTurn,
  onResetTurnOrder,
  className,
}: TurnOrderToolProps) {
  const [isAdvancing, setIsAdvancing] = useState(false);
  const [isResetting, setIsResetting] = useState(false);

  const handleAdvance = async () => {
    if (!onAdvanceTurn || isAdvancing) return;
    setIsAdvancing(true);
    try {
      await onAdvanceTurn();
    } finally {
      setIsAdvancing(false);
    }
  };

  const handleReset = async () => {
    if (!onResetTurnOrder || isResetting) return;
    setIsResetting(true);
    try {
      await onResetTurnOrder();
    } finally {
      setIsResetting(false);
    }
  };

  // ── Loading state ───────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[300px]" aria-busy="true">
        <Loader2 className="w-6 h-6 animate-spin text-amber-600" aria-hidden="true" />
        <span className="sr-only">Caricamento ordine di turno…</span>
      </div>
    );
  }

  // ── Error state ─────────────────────────────────────────────────────────────

  if (error) {
    return (
      <div
        className="flex items-center justify-center min-h-[300px]"
        role="alert"
        aria-live="assertive"
      >
        <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
      </div>
    );
  }

  // ── Uninitialised state ─────────────────────────────────────────────────────

  if (!turnOrder) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[300px] gap-3 text-stone-500 dark:text-stone-400">
        <RotateCcw className="w-8 h-8 opacity-40" aria-hidden="true" />
        <p className="text-sm italic">Ordine di turno non ancora configurato.</p>
      </div>
    );
  }

  // ── Main view ───────────────────────────────────────────────────────────────

  return (
    <section
      className={cn('flex flex-col gap-4 max-w-md mx-auto py-4', className)}
      aria-label="Ordine di turno"
    >
      {/* Header: title + round badge */}
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-base font-semibold text-stone-800 dark:text-stone-200">
          <RotateCcw className="w-4 h-4 text-amber-600 dark:text-amber-400" aria-hidden="true" />
          Ordine di Turno
        </h2>
        <span
          className="px-2.5 py-0.5 text-xs font-semibold rounded-full bg-amber-100 dark:bg-amber-900/40 text-amber-800 dark:text-amber-200 border border-amber-300 dark:border-amber-700"
          aria-label={`Round ${turnOrder.roundNumber}`}
        >
          Round {turnOrder.roundNumber}
        </span>
      </div>

      {/* Player list */}
      <ol
        className="flex flex-col gap-2"
        aria-label="Lista giocatori in ordine di turno"
        aria-live="polite"
        aria-atomic="true"
      >
        {turnOrder.playerOrder.map((playerName: string, index: number) => {
          const isCurrent = index === turnOrder.currentIndex;
          const isNext =
            !isCurrent && index === (turnOrder.currentIndex + 1) % turnOrder.playerOrder.length;

          return (
            <li
              key={playerName}
              className={cn(
                'flex items-center gap-3 px-4 py-3 rounded-lg border transition-all duration-300',
                isCurrent
                  ? 'bg-amber-50 dark:bg-amber-900/30 border-amber-400 dark:border-amber-600 shadow-sm'
                  : 'bg-white dark:bg-stone-800 border-stone-200 dark:border-stone-700'
              )}
              aria-current={isCurrent ? 'step' : undefined}
            >
              {/* Position number */}
              <span
                className={cn(
                  'flex-shrink-0 w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold',
                  isCurrent
                    ? 'bg-amber-500 dark:bg-amber-400 text-white'
                    : 'bg-stone-200 dark:bg-stone-700 text-stone-500 dark:text-stone-400'
                )}
                aria-hidden="true"
              >
                {index + 1}
              </span>

              {/* Player name */}
              <span
                className={cn(
                  'flex-1 text-sm font-medium',
                  isCurrent
                    ? 'text-amber-900 dark:text-amber-100'
                    : 'text-stone-700 dark:text-stone-300'
                )}
              >
                {playerName}
              </span>

              {/* Status badges */}
              {isCurrent && (
                <span className="px-2 py-0.5 text-[10px] font-bold rounded-full bg-amber-500 dark:bg-amber-400 text-white uppercase tracking-wide">
                  Attuale
                </span>
              )}
              {isNext && (
                <span className="flex items-center gap-1 text-[10px] font-medium text-stone-400 dark:text-stone-500">
                  <ChevronRight className="w-3 h-3" aria-hidden="true" />
                  Prossimo
                </span>
              )}
            </li>
          );
        })}
      </ol>

      {/* Host-only action buttons */}
      {isHost && (
        <div className="flex items-center gap-3 pt-2 border-t border-stone-200 dark:border-stone-700">
          {/* Advance turn */}
          {onAdvanceTurn && (
            <button
              type="button"
              onClick={() => void handleAdvance()}
              disabled={isAdvancing}
              className={cn(
                'flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded-lg text-sm font-semibold',
                'bg-amber-600 hover:bg-amber-700 dark:bg-amber-500 dark:hover:bg-amber-400 text-white',
                'transition-colors duration-150',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500 focus-visible:ring-offset-1',
                'disabled:opacity-60 disabled:cursor-not-allowed'
              )}
              aria-label="Fine turno — passa al prossimo giocatore"
            >
              {isAdvancing ? (
                <Loader2 className="w-4 h-4 animate-spin" aria-hidden="true" />
              ) : (
                <ChevronRight className="w-4 h-4" aria-hidden="true" />
              )}
              Fine turno
            </button>
          )}

          {/* Reset */}
          {onResetTurnOrder && (
            <button
              type="button"
              onClick={() => void handleReset()}
              disabled={isResetting}
              className={cn(
                'px-3 py-2 rounded-lg text-sm font-medium',
                'text-stone-600 dark:text-stone-400',
                'border border-stone-300 dark:border-stone-600',
                'hover:bg-stone-100 dark:hover:bg-stone-800',
                'transition-colors duration-150',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500 focus-visible:ring-offset-1',
                'disabled:opacity-60 disabled:cursor-not-allowed'
              )}
              aria-label="Reimposta ordine di turno al round 1"
              title="Reset"
            >
              {isResetting ? (
                <Loader2 className="w-4 h-4 animate-spin" aria-hidden="true" />
              ) : (
                <RefreshCw className="w-4 h-4" aria-hidden="true" />
              )}
            </button>
          )}
        </div>
      )}
    </section>
  );
}
