'use client';

import React from 'react';

import { RotateCcw } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface TurnIndicatorBarProps {
  /** Active player display name (null when TurnOrder tool not connected yet). */
  activePlayerName?: string | null;
  /** Current round number (null when not available). */
  roundNumber?: number | null;
  /** Called when "End Turn" button is pressed. */
  onEndTurn?: () => void;
  /** Whether the end-turn action is available. */
  canEndTurn?: boolean;
  className?: string;
}

/**
 * TurnIndicatorBar — persistent sub-header showing active player + round.
 *
 * Placeholder for Issue #4973; wired to TurnOrder state in Issue #4975.
 * WCAG: aria-live="polite" so screen readers announce turn changes.
 *
 * Issue #4973.
 */
export function TurnIndicatorBar({
  activePlayerName,
  roundNumber,
  onEndTurn,
  canEndTurn = false,
  className,
}: TurnIndicatorBarProps) {
  return (
    <div
      className={cn(
        'flex items-center justify-between px-4 py-1.5',
        'bg-amber-50 dark:bg-amber-950/30 border-b border-amber-200/60 dark:border-amber-800/40',
        'text-amber-900 dark:text-amber-200',
        className,
      )}
      aria-live="polite"
      aria-atomic="true"
    >
      {/* Left: turn info */}
      <div className="flex items-center gap-2 text-sm">
        <RotateCcw className="w-3.5 h-3.5 text-amber-600 dark:text-amber-400 flex-shrink-0" aria-hidden="true" />

        {activePlayerName ? (
          <span>
            <span className="text-amber-600 dark:text-amber-400 font-medium">Turn: </span>
            <span className="font-semibold">{activePlayerName}</span>
            {roundNumber != null && (
              <span className="ml-2 text-amber-700/70 dark:text-amber-400/70">
                · Round {roundNumber}
              </span>
            )}
          </span>
        ) : (
          <span className="text-amber-700/60 dark:text-amber-400/60 italic text-xs">
            Turn order not configured
          </span>
        )}
      </div>

      {/* Right: End Turn CTA */}
      {canEndTurn && onEndTurn && (
        <button
          type="button"
          onClick={onEndTurn}
          className={cn(
            'px-3 py-0.5 text-xs font-semibold rounded-full',
            'bg-amber-600 hover:bg-amber-700 dark:bg-amber-500 dark:hover:bg-amber-400',
            'text-white transition-colors duration-150',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-400 focus-visible:ring-offset-1',
          )}
          aria-label="End current turn"
        >
          End Turn →
        </button>
      )}
    </div>
  );
}
