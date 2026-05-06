/**
 * ScoringInline — Wave D.1 v2 component (Issue #735).
 *
 * Mapped from `admin-mockups/design_files/sp4-sessions-index.jsx` (ScoringInline).
 *
 * Pure component: no fetch, no i18n calls. Displays player scores inline.
 *
 * Schema reality (v1 carryover): when all scores === 0 (SessionPlayerDto has no
 * score field — placeholder 0), the score chip is hidden and only player names
 * are shown. This avoids a confusing "everyone scored 0" display.
 *
 * Compact mode: max 3 scores visible + "+N others" overflow indicator.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { SessionScoreEntry } from '@/lib/sessions/sessions-filters';

export interface ScoringInlineLabels {
  readonly winnerAriaLabel: string;
  /** Template "{count} altri", e.g. "+2 altri" */
  readonly overflowTemplate?: string;
}

export interface ScoringInlineProps {
  readonly scores: ReadonlyArray<SessionScoreEntry>;
  readonly compact?: boolean;
  readonly labels: ScoringInlineLabels;
  readonly className?: string;
}

/** Returns true when every score is 0 (v1 carryover placeholder). */
function allScoresZero(scores: ReadonlyArray<SessionScoreEntry>): boolean {
  return scores.length > 0 && scores.every(s => s.score === 0);
}

export function ScoringInline({
  scores,
  compact = false,
  labels,
  className,
}: ScoringInlineProps): ReactElement | null {
  if (scores.length === 0) return null;

  const hideScores = allScoresZero(scores);
  const visibleScores = compact ? scores.slice(0, 3) : scores;
  const overflowCount = compact ? scores.length - 3 : 0;

  return (
    <div
      data-slot="scoring-inline"
      className={clsx('flex flex-wrap items-center gap-x-2 gap-y-1', className)}
      aria-label={labels.winnerAriaLabel}
    >
      {visibleScores.map((entry, idx) => (
        <span
          key={`${entry.name}-${idx}`}
          className={clsx(
            'inline-flex items-center gap-1',
            compact ? 'text-[10.5px]' : 'text-[11.5px]',
            'font-mono tabular-nums',
            entry.winner
              ? 'font-extrabold text-[hsl(240,60%,45%)]'
              : 'font-semibold text-muted-foreground'
          )}
        >
          {entry.winner && (
            <span aria-hidden="true" className={compact ? 'text-[9px]' : 'text-[10px]'}>
              🏆
            </span>
          )}
          <span>{entry.name}</span>
          {!hideScores && (
            <span
              className={clsx(
                'rounded px-1 py-px font-extrabold',
                entry.winner ? 'bg-[hsla(240,60%,55%,0.14)]' : 'bg-muted text-muted-foreground'
              )}
            >
              {entry.score}
            </span>
          )}
          {entry.note && <span className="text-[9px] text-muted-foreground">({entry.note})</span>}
        </span>
      ))}

      {overflowCount > 0 && (
        <span className="font-mono text-[9.5px] font-bold text-muted-foreground">
          {labels.overflowTemplate
            ? labels.overflowTemplate.replace('{count}', String(overflowCount))
            : `+${overflowCount}`}
        </span>
      )}
    </div>
  );
}
