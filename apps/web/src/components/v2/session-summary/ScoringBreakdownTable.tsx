/**
 * ScoringBreakdownTable — Wave D.3 v2 component (Issue #756).
 *
 * Vertical table listing all participants ranked by score. Tied rows are
 * marked with `data-tied="true"` and an `=` indicator. The 1st-place row
 * (or all tied 1st rows) gets a soft toolkit-color background highlight.
 *
 * Mockup mapping:
 *   - admin-mockups/design_files/sp4-session-summary-parts.jsx (ScoringBreakdownTable)
 *
 * Contract reference: docs/frontend/contracts/sessions-id-summary-hooks.md §5.5.
 *
 * MeepleCard divergence (Gate C): table layout with semantic `<table>`,
 * `<th scope>` headers, and aria-sort. MeepleCard cannot host tabular data —
 * it's a card surface, not a tabular grid. DIVERGE.
 *
 * A11y:
 *   - `<table>` with `<caption>` (sr-only — visual title in parent section).
 *   - `<th scope="col">` for column headers, `<th scope="row">` for the
 *     participant name column.
 *   - `aria-sort="descending"` on the score column (sort is fixed by rank
 *     in v1; future epic may add interactive sort).
 *
 * Pure component: orchestrator resolves all i18n strings via `labels`.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { RankedParticipant } from '@/lib/sessions-summary/tie-groups';

export interface ScoringBreakdownTableLabels {
  readonly title: string;
  readonly headerName: string;
  readonly headerScore: string;
  readonly headerRank: string;
  /** Visual indicator (e.g. "Pari merito") used as a label suffix on tied rows. */
  readonly tied: string;
}

export interface ScoringBreakdownTableProps {
  readonly rankedParticipants: readonly RankedParticipant[];
  readonly labels: ScoringBreakdownTableLabels;
  readonly className?: string;
}

export function ScoringBreakdownTable({
  rankedParticipants,
  labels,
  className,
}: ScoringBreakdownTableProps): ReactElement {
  return (
    <div
      data-slot="scoring-breakdown-table"
      className={clsx('overflow-hidden rounded-lg border border-border bg-card', className)}
    >
      <div className="overflow-x-auto">
        <table className="w-full border-collapse">
          <caption className="sr-only">{labels.title}</caption>
          <thead>
            <tr className="bg-muted/40">
              <th
                scope="col"
                className="px-3 py-2 text-left font-mono text-[10px] font-extrabold uppercase tracking-wide text-muted-foreground"
              >
                {labels.headerRank}
              </th>
              <th
                scope="col"
                className="px-3 py-2 text-left font-mono text-[10px] font-extrabold uppercase tracking-wide text-muted-foreground"
              >
                {labels.headerName}
              </th>
              <th
                scope="col"
                aria-sort="descending"
                className="px-3 py-2 text-right font-mono text-[10px] font-extrabold uppercase tracking-wide text-muted-foreground"
              >
                {labels.headerScore}
              </th>
            </tr>
          </thead>
          <tbody>
            {rankedParticipants.map(p => {
              const isWinner = p.rank === 1;
              return (
                <tr
                  key={p.id}
                  data-slot="scoring-row"
                  data-rank={p.rank}
                  data-tied={p.isTied || undefined}
                  className={clsx(
                    'border-t border-border/60 transition-colors',
                    isWinner && 'bg-[hsla(142,70%,31%,0.06)]'
                  )}
                >
                  <td className="whitespace-nowrap px-3 py-2.5 font-mono text-xs font-extrabold tabular-nums">
                    <span className="inline-flex items-center gap-1">
                      {isWinner && (
                        <span aria-hidden="true" className="text-sm">
                          🏆
                        </span>
                      )}
                      {p.rank}°
                      {p.isTied && (
                        <span
                          aria-label={labels.tied}
                          className="ml-1 inline-flex items-center text-[hsl(142,70%,31%)]"
                          data-slot="scoring-tied-indicator"
                        >
                          =
                        </span>
                      )}
                    </span>
                  </td>
                  <th
                    scope="row"
                    className={clsx(
                      'px-3 py-2.5 text-left text-sm',
                      isWinner ? 'font-extrabold text-foreground' : 'font-semibold text-foreground'
                    )}
                  >
                    {p.displayName}
                  </th>
                  <td
                    className={clsx(
                      'px-3 py-2.5 text-right tabular-nums',
                      isWinner
                        ? 'font-display text-base font-extrabold text-[hsl(142,70%,31%)]'
                        : 'font-display text-base font-extrabold text-foreground'
                    )}
                  >
                    {p.totalScore}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
