/**
 * Stars — fractional rating display.
 *
 * Renders a 5-star scale with sub-step granularity via two stacked rows
 * (background empty + foreground clipped via `width`). Pure presentational.
 *
 * Canonical location post-#1469: `apps/web/src/components/ui/feedback/Stars.tsx`.
 * Previously lived at `apps/web/src/components/features/toolkit-detail/Stars.tsx`
 * (introduced in Wave 3 #1145); that path now re-exports from this module to
 * preserve backwards compatibility while making the primitive reusable
 * cross-feature (game-detail, player-detail, etc.).
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export interface StarsProps {
  /** 0.0–5.0; values outside the range are clamped. */
  readonly value: number;
  /** When true, renders the numeric value adjacent (e.g. "4.7"). */
  readonly showNumeric?: boolean;
  /** Accessible label. Defaults to `"Rating: {value} / 5"`. */
  readonly ariaLabel?: string;
  readonly className?: string;
}

const MAX = 5;

export function Stars({ value, showNumeric, ariaLabel, className }: StarsProps): JSX.Element {
  const clamped = Math.max(0, Math.min(MAX, Number.isFinite(value) ? value : 0));
  const percent = (clamped / MAX) * 100;
  const label = ariaLabel ?? `Rating: ${clamped.toFixed(1)} / ${MAX}`;

  return (
    <span
      data-slot="stars"
      role="img"
      aria-label={label}
      className={clsx('inline-flex items-center gap-1.5', className)}
    >
      <span className="relative inline-block leading-none">
        <span aria-hidden="true" className="text-base text-muted-foreground/40">
          ★★★★★
        </span>
        <span
          aria-hidden="true"
          className="absolute inset-0 overflow-hidden text-base text-amber-500"
          style={{ width: `${percent}%` }}
        >
          ★★★★★
        </span>
      </span>
      {showNumeric && (
        <span className="font-mono text-xs text-muted-foreground tabular-nums">
          {clamped.toFixed(1)}
        </span>
      )}
    </span>
  );
}
