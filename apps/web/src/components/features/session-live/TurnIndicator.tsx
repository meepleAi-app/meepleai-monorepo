/**
 * TurnIndicator — Wave D.2 Foundation sub-PR (Issue #746).
 *
 * Displays current turn progress and active player.
 * A11y: role="progressbar" aria-valuenow/min/max per spec §5.2.
 *
 * Gate C: DIVERGES from MeepleCard — live turn progress, not a card.
 */

import type { ReactElement } from 'react';

// ─── Labels ───────────────────────────────────────────────────────────────────

export interface TurnIndicatorLabels {
  /** Raw template "{current} di {total}" — component does .replace() for aria. */
  readonly currentTurnAriaLabel: string;
  /** Raw template "{playerName}" — component does .replace() for display. */
  readonly activePlayerLabel: string;
  readonly yourTurnLabel: string;
  readonly waitingLabel: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface TurnIndicatorProps {
  readonly current: number;
  readonly total: number;
  readonly activePlayerName: string;
  readonly isMyTurn: boolean;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorLabels;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function TurnIndicator({
  current,
  total,
  activePlayerName,
  isMyTurn,
  compact = false,
  labels,
}: TurnIndicatorProps): ReactElement {
  const progressPercent = total > 0 ? Math.round((current / total) * 100) : 0;
  const ariaLabel = labels.currentTurnAriaLabel
    .replace('{current}', String(current))
    .replace('{total}', String(total));
  const playerDisplay = labels.activePlayerLabel.replace('{playerName}', activePlayerName);

  return (
    <div
      data-slot="turn-indicator"
      className={compact ? 'flex flex-col gap-1' : 'flex flex-col gap-2'}
    >
      {/* Turn counter */}
      <div className="flex items-center justify-between gap-2">
        <span
          className={['font-semibold text-slate-100', compact ? 'text-xs' : 'text-sm'].join(' ')}
          aria-hidden="true"
        >
          {current}/{total}
        </span>
        <span
          className={[
            'font-medium',
            isMyTurn ? 'text-emerald-400' : 'text-muted-foreground',
            compact ? 'text-xs' : 'text-xs',
          ].join(' ')}
        >
          {isMyTurn ? labels.yourTurnLabel : labels.waitingLabel}
        </span>
      </div>

      {/* Progress bar */}
      <div
        role="progressbar"
        aria-valuenow={current}
        aria-valuemin={1}
        aria-valuemax={total}
        aria-label={ariaLabel}
        className="h-1.5 w-full overflow-hidden rounded-full bg-card"
      >
        <div
          // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: session hue in Tailwind arbitrary class (progress bar); no dark-bg light entity token exists
          className="h-full rounded-full bg-[hsl(240,60%,65%)] transition-[width]"
          style={{ width: `${progressPercent}%` }}
          aria-hidden="true"
        />
      </div>

      {/* Active player */}
      {!compact && activePlayerName && (
        <p className="truncate text-xs text-muted-foreground" aria-label={playerDisplay}>
          {playerDisplay}
        </p>
      )}
    </div>
  );
}
