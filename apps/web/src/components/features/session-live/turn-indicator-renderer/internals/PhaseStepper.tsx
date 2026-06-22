'use client';

/**
 * PhaseStepper — Issue #2378 G5b internal helper.
 *
 * Renders a sequential or custom turn-order phase list with
 * visual current/past/future step states.
 *
 * Used by: SequentialBranch, CustomBranch.
 */

import type { ReactElement } from 'react';

interface Props {
  readonly phases: ReadonlyArray<string>;
  readonly activeIndex: number;
}

export function PhaseStepper({ phases, activeIndex }: Props): ReactElement {
  if (phases.length === 0) {
    return <p className="text-xs text-muted-foreground italic">Nessuna fase configurata.</p>;
  }
  return (
    <ol className="flex flex-col gap-0">
      {phases.map((phase, i) => {
        const isActive = i === activeIndex;
        const isPast = i < activeIndex;
        return (
          <li
            key={phase}
            aria-current={isActive ? 'step' : undefined}
            className="flex items-center gap-3 py-1"
          >
            <span
              className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-bold ${
                isActive
                  ? 'bg-entity-player text-white'
                  : isPast
                    ? 'bg-entity-player/20 text-entity-player'
                    : 'bg-card text-muted-foreground'
              }`}
            >
              {isPast ? '✓' : i + 1}
            </span>
            <span
              className={`text-sm ${
                isActive
                  ? 'font-bold text-entity-player'
                  : isPast
                    ? 'font-medium text-foreground'
                    : 'text-muted-foreground'
              }`}
            >
              {phase}
            </span>
          </li>
        );
      })}
    </ol>
  );
}
