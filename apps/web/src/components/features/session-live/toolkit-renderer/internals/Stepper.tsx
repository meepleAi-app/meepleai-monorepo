'use client';

/**
 * Stepper — Issue #2376 G5c internal helper.
 *
 * Numeric +/- control shared by ScoreTracker and ResourceManager widgets.
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2376-g5c-toolkit-renderer-design.md §3
 */

import type { ReactElement } from 'react';

export interface StepperProps {
  readonly value: number;
  readonly onChange: (next: number) => void;
  readonly min?: number;
  readonly max?: number;
  readonly step?: number;
  readonly incrementAriaLabel: string;
  readonly decrementAriaLabel: string;
  readonly wide?: boolean;
  readonly variant?: 'tool' | 'player' | 'session' | 'kb' | 'event';
}

export function Stepper({
  value,
  onChange,
  min,
  max,
  step = 1,
  incrementAriaLabel,
  decrementAriaLabel,
  wide = false,
  variant = 'session',
}: StepperProps): ReactElement {
  const canDecrement = min === undefined || value - step >= min;
  const canIncrement = max === undefined || value + step <= max;

  const accentMap: Record<string, string> = {
    tool: 'border-entity-tool/40 bg-entity-tool/10 text-entity-tool hover:bg-entity-tool/20',
    player:
      'border-entity-player/40 bg-entity-player/10 text-entity-player hover:bg-entity-player/20',
    session:
      'border-entity-session/40 bg-entity-session/10 text-entity-session hover:bg-entity-session/20',
    kb: 'border-entity-kb/40 bg-entity-kb/10 text-entity-kb hover:bg-entity-kb/20',
    event: 'border-entity-event/40 bg-entity-event/10 text-entity-event hover:bg-entity-event/20',
  };
  const incrementAccent = accentMap[variant] ?? accentMap.session;

  return (
    <div data-slot="stepper" className="flex items-center gap-1.5">
      <button
        type="button"
        aria-label={decrementAriaLabel}
        disabled={!canDecrement}
        onClick={() => onChange(value - step)}
        className="flex h-6 w-6 items-center justify-center rounded border border-border bg-muted text-sm font-bold text-muted-foreground transition-colors hover:bg-muted/80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-40"
      >
        −
      </button>
      <span
        className={`text-center text-sm font-bold tabular-nums text-foreground ${
          wide ? 'min-w-[2rem]' : 'min-w-[1.5rem]'
        }`}
      >
        {value}
      </span>
      <button
        type="button"
        aria-label={incrementAriaLabel}
        disabled={!canIncrement}
        onClick={() => onChange(value + step)}
        className={`flex h-6 w-6 items-center justify-center rounded border text-sm font-bold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-40 ${incrementAccent}`}
      >
        +
      </button>
    </div>
  );
}
