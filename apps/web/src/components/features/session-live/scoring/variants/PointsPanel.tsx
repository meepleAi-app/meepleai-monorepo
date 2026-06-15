/**
 * PointsPanel — read-side leaderboard for `ScoreType=Points`.
 *
 * Composed by `ScoringPanelRenderer` when `data.scoringType === 'Points'`.
 * Pure read-side: scores are projected from the orchestrator's
 * `PointsPanelData`. WRITE-side editing is delegated to `PolymorphicScoreEditor`
 * by the parent dispatcher (host gate).
 *
 * Visual contract anchors:
 * - `admin-mockups/design_files/sp4-session-skeleton-renderers.jsx` lines 100-160
 *   (Points variant — leader pill + tabular-nums score + category breakdown).
 *
 * Token discipline (CLAUDE.md § Token Canonicalization, DS-15 error level):
 * - Leader accent: `text-entity-toolkit` (NOT `text-amber-*`).
 * - Surfaces: `bg-card` / `bg-muted` / `border-border`. No `bg-white`,
 *   `text-gray-*`, `bg-slate-*`, raw HSL.
 *
 * Issue #2373 — sub-issue G5a of epic #2354 (T2).
 */

import type { ReactElement } from 'react';

import type { PointsPanelData } from '../types';

export interface PointsPanelLabels {
  readonly title: string;
  readonly emptyMessage: string;
  /** ARIA-only suffix appended to the leader row label (e.g. " (leader)"). */
  readonly leaderAriaSuffix: string;
  readonly categoriesTitle: string;
  /** Prefix for the turnDelta badge (default "+"). */
  readonly turnDeltaPrefix: string;
}

export interface PointsPanelProps {
  readonly data: PointsPanelData;
  readonly labels: PointsPanelLabels;
  readonly className?: string;
}

export function PointsPanel({ data, labels, className }: PointsPanelProps): ReactElement {
  // Defensive copy: do NOT mutate caller-owned arrays. The orchestrator MAY
  // pass an already-sorted snapshot but we re-sort here so the component is
  // self-contained for Storybook / fixture rendering.
  const sorted = [...data.players].sort((a, b) => (b.score ?? 0) - (a.score ?? 0));

  const categories = data.categories ?? [];
  const hasCategories = categories.length > 0;
  const isEmpty = sorted.length === 0;

  return (
    <section
      data-testid="scoring-panel-points"
      data-score-type="Points"
      aria-label={labels.title}
      className={`flex flex-col gap-3 rounded-lg border border-border bg-card p-3 ${className ?? ''}`}
    >
      <h3 className="text-sm font-semibold text-foreground">{labels.title}</h3>

      {isEmpty ? (
        <p
          data-testid="points-empty"
          className="rounded-md bg-muted/40 px-3 py-4 text-center text-sm text-muted-foreground"
        >
          {labels.emptyMessage}
        </p>
      ) : (
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => {
            const isLeader = idx === 0;
            const turnDelta = player.turnDelta;
            const showDelta = typeof turnDelta === 'number' && turnDelta !== 0;
            const nameClass = isLeader
              ? 'truncate text-sm font-semibold text-entity-toolkit'
              : 'truncate text-sm font-medium text-foreground';

            return (
              <li
                key={player.id}
                data-leader={isLeader ? 'true' : 'false'}
                data-player-id={player.id}
                className={[
                  'flex items-center justify-between gap-2 rounded-md px-2 py-1.5',
                  isLeader ? 'bg-entity-toolkit/10' : 'bg-muted/30',
                ].join(' ')}
                aria-label={
                  isLeader
                    ? `${player.displayName} (${labels.leaderAriaSuffix})`
                    : player.displayName
                }
              >
                <div className="flex min-w-0 items-center gap-2">
                  <span
                    aria-hidden="true"
                    className={[
                      'shrink-0 tabular-nums text-xs font-medium',
                      isLeader ? 'text-entity-toolkit' : 'text-muted-foreground',
                    ].join(' ')}
                  >
                    #{idx + 1}
                  </span>
                  <span className={nameClass}>{player.displayName}</span>
                  {showDelta && (
                    <span
                      data-testid="points-turn-delta"
                      className="shrink-0 rounded-full bg-entity-toolkit/15 px-1.5 py-0.5
                        text-xs font-semibold tabular-nums text-entity-toolkit"
                    >
                      {labels.turnDeltaPrefix}
                      {turnDelta}
                    </span>
                  )}
                </div>

                <span
                  data-testid="points-score-value"
                  className="min-w-[2.5rem] text-right tabular-nums text-base font-bold text-foreground"
                >
                  {player.score ?? 0}
                </span>
              </li>
            );
          })}
        </ul>
      )}

      {hasCategories && (
        <div
          data-testid="points-categories"
          className="flex flex-col gap-2 border-t border-border pt-2"
        >
          <h4 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            {labels.categoriesTitle}
          </h4>
          <ul role="list" className="flex flex-col gap-1">
            {categories.map(cat => (
              <li
                key={cat.id}
                className="flex items-center justify-between gap-2 rounded px-2 py-1
                  text-xs"
              >
                <span className="truncate text-foreground">{cat.label}</span>
                <span
                  className="shrink-0 rounded-full bg-muted px-1.5 py-0.5
                    font-mono text-[10px] uppercase tracking-wider text-muted-foreground"
                >
                  {cat.computation}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
