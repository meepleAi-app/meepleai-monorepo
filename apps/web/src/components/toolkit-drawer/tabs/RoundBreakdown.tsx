'use client';

/**
 * RoundBreakdown — Expandable per-round score breakdown (placeholder structure).
 *
 * Note: Since local scores are currently stored as totals (not per-round deltas),
 * the breakdown only shows the current round totals. Full per-round tracking
 * would require storing score history — future enhancement.
 */

import React, { useState } from 'react';

import { cn } from '@/lib/utils';

export interface RoundBreakdownProps {
  currentRound: number;
  onAdvanceRound: () => void;
}

export function RoundBreakdown({ currentRound, onAdvanceRound }: RoundBreakdownProps) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="rounded-lg border border-border bg-muted" data-testid="round-breakdown">
      <button
        type="button"
        onClick={() => setExpanded(v => !v)}
        className="flex w-full items-center justify-between px-3 py-2 text-xs font-semibold text-muted-foreground hover:bg-muted"
        data-testid="round-breakdown-toggle"
      >
        <span className="flex items-center gap-1">
          <span className={cn('transition-transform', expanded && 'rotate-90')}>&#9654;</span>
          📊 Dettaglio round — R{currentRound}
        </span>
      </button>
      {expanded && (
        <div className="border-t border-border p-3">
          <p className="mb-2 text-[11px] text-muted-foreground">
            Round corrente: <strong>R{currentRound}</strong>
          </p>
          <button
            type="button"
            onClick={onAdvanceRound}
            className="w-full rounded-lg bg-[hsl(142,70%,45%)] py-1.5 text-xs font-semibold text-white hover:bg-[hsl(142,70%,40%)]"
            data-testid="advance-round-btn"
          >
            Nuovo Round → R{currentRound + 1}
          </button>
        </div>
      )}
    </div>
  );
}
