/**
 * RankingPanel — read-side ordered ranking for `ScoreType=Ranking`.
 *
 * Composed by `ScoringPanelRenderer` when `data.scoringType === 'Ranking'`.
 * Pure read-side: ranks are projected from the orchestrator's
 * `RankingPanelData`. WRITE-side editing is delegated to `PolymorphicScoreEditor`
 * by the parent dispatcher (host gate).
 *
 * The component DOES NOT re-sort — it trusts `data.ranking` to be sorted
 * ascending by `rank` (invariant declared in `RankingPanelData`). The
 * orchestrator adapter is responsible for sort discipline.
 *
 * Visual contract anchors:
 * - `admin-mockups/design_files/sp4-session-skeleton-renderers.jsx` lines 161-200
 *   (Ranking variant — rank pill + Trophy on leader + sub line).
 *
 * Token discipline (CLAUDE.md § Token Canonicalization, DS-15 error level):
 * - Leader pill: `bg-entity-toolkit text-primary-foreground` (NOT amber).
 * - Surfaces: `bg-card` / `bg-muted` / `border-border`. No raw HSL.
 *
 * Issue #2373 — sub-issue G5a of epic #2354 (T3).
 */

import type { ReactElement } from 'react';

import type { RankingPanelData } from '../types';

export interface RankingPanelLabels {
  readonly title: string;
  readonly emptyMessage: string;
  readonly leaderAriaSuffix: string;
  /** Template: "Posizione {rank}" — component does `.replace('{rank}', n)`. */
  readonly rankAriaLabelTemplate: string;
  readonly trophyAriaLabel: string;
}

export interface RankingPanelProps {
  readonly data: RankingPanelData;
  readonly labels: RankingPanelLabels;
  readonly className?: string;
}

/**
 * Inline SVG trophy icon. Inlined (vs lucide-react import) to:
 *   1. avoid a runtime dep added only for one icon, and
 *   2. inherit `color` from `text-primary-foreground` on the leader pill so we
 *      do not have to thread a separate stroke prop.
 */
function TrophyIcon({ label }: { readonly label: string }): ReactElement {
  return (
    <svg
      role="img"
      aria-label={label}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      className="h-3 w-3 shrink-0"
    >
      <path d="M6 9H4.5a2.5 2.5 0 0 1 0-5H6" />
      <path d="M18 9h1.5a2.5 2.5 0 0 0 0-5H18" />
      <path d="M4 22h16" />
      <path d="M10 14.66V17c0 .55-.47.98-.97 1.21C7.85 18.75 7 20.24 7 22" />
      <path d="M14 14.66V17c0 .55.47.98.97 1.21C16.15 18.75 17 20.24 17 22" />
      <path d="M18 2H6v7a6 6 0 0 0 12 0V2Z" />
    </svg>
  );
}

export function RankingPanel({ data, labels, className }: RankingPanelProps): ReactElement {
  const isEmpty = data.ranking.length === 0;

  return (
    <section
      data-testid="scoring-panel-ranking"
      data-score-type="Ranking"
      aria-label={labels.title}
      className={`flex flex-col gap-3 rounded-lg border border-border bg-card p-3 ${className ?? ''}`}
    >
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="text-sm font-semibold text-foreground">{labels.title}</h3>
        {data.meta && (
          <span data-testid="ranking-meta" className="text-xs font-medium text-muted-foreground">
            {data.meta}
          </span>
        )}
      </div>

      {isEmpty ? (
        <p
          data-testid="ranking-empty"
          className="rounded-md bg-muted/40 px-3 py-4 text-center text-sm text-muted-foreground"
        >
          {labels.emptyMessage}
        </p>
      ) : (
        <ol role="list" className="flex flex-col gap-1">
          {data.ranking.map(entry => {
            const isLeader = entry.rank === 1;
            const rankAriaLabel = labels.rankAriaLabelTemplate.replace(
              '{rank}',
              String(entry.rank)
            );

            return (
              <li
                key={entry.id}
                data-leader={isLeader ? 'true' : 'false'}
                data-player-id={entry.id}
                className={[
                  'flex items-center gap-3 rounded-md px-2 py-1.5',
                  isLeader ? 'bg-entity-toolkit/10' : 'bg-muted/30',
                ].join(' ')}
                aria-label={
                  isLeader ? `${entry.displayName} (${labels.leaderAriaSuffix})` : entry.displayName
                }
              >
                <span
                  data-testid="ranking-rank-pill"
                  aria-label={rankAriaLabel}
                  className={[
                    'inline-flex h-6 min-w-[1.5rem] shrink-0 items-center justify-center gap-1',
                    'rounded-full px-2 text-xs font-bold tabular-nums',
                    isLeader
                      ? 'bg-entity-toolkit text-primary-foreground'
                      : 'bg-muted text-muted-foreground',
                  ].join(' ')}
                >
                  {isLeader && <TrophyIcon label={labels.trophyAriaLabel} />}
                  <span>{entry.rank}</span>
                </span>

                <div className="flex min-w-0 flex-col">
                  <span
                    className={[
                      'truncate text-sm',
                      isLeader ? 'font-semibold text-foreground' : 'font-medium text-foreground',
                    ].join(' ')}
                  >
                    {entry.displayName}
                  </span>
                  {entry.sub && (
                    <span
                      data-testid="ranking-sub"
                      className="truncate text-xs text-muted-foreground"
                    >
                      {entry.sub}
                    </span>
                  )}
                </div>
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}
