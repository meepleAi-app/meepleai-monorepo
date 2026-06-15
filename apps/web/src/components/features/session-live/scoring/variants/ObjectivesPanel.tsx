/**
 * ObjectivesPanel — read-side checklist for `ScoreType=Objectives`.
 *
 * Composed by `ScoringPanelRenderer` when `data.scoringType === 'Objectives'`.
 * Pure read-side: the checklist is projected from the orchestrator's
 * `ObjectivesPanelData`. WRITE-side editing is delegated to
 * `PolymorphicScoreEditor` by the parent dispatcher (host-only).
 *
 * Visual contract anchors:
 * - `admin-mockups/design_files/sp4-session-skeleton-renderers.jsx` lines 261-286
 *   (Objectives variant — completion counter + progress meter + checklist
 *   with line-through strike on completed items).
 *
 * Token discipline (CLAUDE.md § Token Canonicalization, DS-15 error level):
 * - Completed accent: `text-entity-toolkit` + checkbox bg `bg-entity-toolkit`.
 * - Surfaces: `bg-card` / `bg-muted` / `border-border`. No raw HSL.
 *
 * Issue #2373 — sub-issue G5a of epic #2354 (T5).
 */

import type { ReactElement } from 'react';

import type { ObjectivesPanelData } from '../types';

export interface ObjectivesPanelLabels {
  readonly title: string;
  readonly emptyMessage: string;
  /** Template: "{done}/{total} completati" — component does `.replace()`. */
  readonly completedCounterTemplate: string;
  readonly doneAriaLabel: string;
  readonly pendingAriaLabel: string;
  /** Template: "Progresso {value}" — component does `.replace()`. */
  readonly progressAriaLabelTemplate: string;
}

export interface ObjectivesPanelProps {
  readonly data: ObjectivesPanelData;
  readonly labels: ObjectivesPanelLabels;
  readonly className?: string;
}

function CheckboxIcon({
  done,
  label,
}: {
  readonly done: boolean;
  readonly label: string;
}): ReactElement {
  if (done) {
    return (
      <svg
        role="img"
        aria-label={label}
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth={3}
        strokeLinecap="round"
        strokeLinejoin="round"
        className="h-3.5 w-3.5"
      >
        <polyline points="20 6 9 17 4 12" />
      </svg>
    );
  }
  return (
    <span
      role="img"
      aria-label={label}
      className="block h-3.5 w-3.5 rounded-sm border-2 border-current"
    />
  );
}

export function ObjectivesPanel({ data, labels, className }: ObjectivesPanelProps): ReactElement {
  const total = data.objectives.length;
  const done = data.objectives.filter(o => o.done).length;
  const isEmpty = total === 0;
  const counterText = labels.completedCounterTemplate
    .replace('{done}', String(done))
    .replace('{total}', String(total));
  const safeMax = Math.max(total, 1);
  const pct = isEmpty ? 0 : (done / safeMax) * 100;

  return (
    <section
      data-testid="scoring-panel-objectives"
      data-score-type="Objectives"
      aria-label={labels.title}
      className={`flex flex-col gap-3 rounded-lg border border-border bg-card p-3 ${className ?? ''}`}
    >
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="text-sm font-semibold text-foreground">{labels.title}</h3>
        {data.meta && (
          <span data-testid="objectives-meta" className="text-xs font-medium text-muted-foreground">
            {data.meta}
          </span>
        )}
      </div>

      {isEmpty ? (
        <p
          data-testid="objectives-empty"
          className="rounded-md bg-muted/40 px-3 py-4 text-center text-sm text-muted-foreground"
        >
          {labels.emptyMessage}
        </p>
      ) : (
        <>
          <div className="flex items-center gap-2">
            <span
              data-testid="objectives-counter"
              className="shrink-0 tabular-nums text-xs font-semibold text-foreground"
            >
              {counterText}
            </span>
            <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-muted">
              <div
                role="progressbar"
                aria-valuenow={done}
                aria-valuemin={0}
                aria-valuemax={total}
                aria-label={counterText}
                className="h-full bg-entity-toolkit transition-all duration-300"
                style={{ width: `${pct}%` }}
              />
            </div>
          </div>

          <ul role="list" className="flex flex-col gap-1">
            {data.objectives.map(obj => {
              const ariaLabel = obj.done ? labels.doneAriaLabel : labels.pendingAriaLabel;
              const labelClass = obj.done
                ? 'truncate text-sm text-muted-foreground line-through'
                : 'truncate text-sm text-foreground';

              return (
                <li
                  key={obj.id}
                  data-done={obj.done ? 'true' : 'false'}
                  data-objective-id={obj.id}
                  className={[
                    'flex items-center gap-2 rounded-md px-2 py-1.5',
                    obj.done ? 'bg-entity-toolkit/8 opacity-65' : 'bg-muted/30',
                  ].join(' ')}
                >
                  <span
                    className={[
                      'flex h-4 w-4 shrink-0 items-center justify-center',
                      obj.done ? 'text-entity-toolkit' : 'text-muted-foreground',
                    ].join(' ')}
                  >
                    <CheckboxIcon done={obj.done} label={ariaLabel} />
                  </span>
                  <span className={labelClass}>{obj.label}</span>
                  {obj.progress && (
                    <span
                      data-testid="objectives-progress"
                      className="ml-auto shrink-0 font-mono text-xs tabular-nums text-muted-foreground"
                    >
                      {obj.progress}
                    </span>
                  )}
                </li>
              );
            })}
          </ul>
        </>
      )}
    </section>
  );
}
