/**
 * BinaryWinPanel — read-side collective outcome for `ScoreType=BinaryWin`.
 *
 * Composed by `ScoringPanelRenderer` when `data.scoringType === 'BinaryWin'`.
 * Pure read-side: collective state is projected from the orchestrator's
 * `BinaryWinPanelData`. WRITE-side editing is delegated to
 * `PolymorphicScoreEditor` by the parent dispatcher (host gate; non-hosts
 * cannot mutate co-op outcomes).
 *
 * Visual contract anchors:
 * - `admin-mockups/design_files/sp4-session-skeleton-renderers.jsx` lines 201-260
 *   (BinaryWin variant — two meters + conditions list with weight badges).
 *
 * Token discipline (CLAUDE.md § Token Canonicalization, DS-15 error level):
 * - Goal meter accent: `bg-entity-toolkit` (positive progress).
 * - Fail meter accent: `bg-entity-event` (danger / failure progress).
 * - Surfaces: `bg-card` / `bg-muted` / `border-border`. No raw HSL.
 *
 * Issue #2373 — sub-issue G5a of epic #2354 (T4).
 */

import type { ReactElement } from 'react';

import type { BinaryWinPanelData } from '../types';

export interface BinaryWinPanelLabels {
  readonly title: string;
  readonly emptyMessage: string;
  readonly categoriesTitle: string;
  readonly weightWinLabel: string;
  readonly weightLoseLabel: string;
  readonly weightNeutralLabel: string;
  /** Template: "Progresso {value} su {max}" — component does `.replace()`. */
  readonly meterAriaLabelTemplate: string;
}

export interface BinaryWinPanelProps {
  readonly data: BinaryWinPanelData;
  readonly labels: BinaryWinPanelLabels;
  readonly className?: string;
}

interface MeterProps {
  readonly variant: 'goal' | 'fail';
  readonly label: string;
  readonly value: number;
  readonly max: number;
  readonly hint?: string;
  readonly ariaLabel: string;
}

function Meter({ variant, label, value, max, hint, ariaLabel }: MeterProps): ReactElement {
  const safeMax = Math.max(max, 1);
  const pct = Math.max(0, Math.min(100, (value / safeMax) * 100));
  const barBg = variant === 'goal' ? 'bg-entity-toolkit' : 'bg-entity-event';

  return (
    <div
      data-testid={`binarywin-${variant}-meter`}
      className="flex flex-col gap-1.5 rounded-md bg-muted/30 px-3 py-2"
    >
      <div className="flex items-baseline justify-between gap-2">
        <span className="text-sm font-medium text-foreground">{label}</span>
        <span className="tabular-nums text-sm font-bold text-foreground">
          {value}/{max}
        </span>
      </div>
      <div className="h-2 w-full overflow-hidden rounded-full bg-muted">
        <div
          data-testid={`binarywin-${variant}-bar`}
          role="progressbar"
          aria-valuenow={value}
          aria-valuemin={0}
          aria-valuemax={max}
          aria-label={ariaLabel}
          className={`h-full ${barBg} transition-all duration-300`}
          style={{ width: `${pct}%` }}
        />
      </div>
      {hint && (
        <p data-testid={`binarywin-${variant}-hint`} className="text-xs text-muted-foreground">
          {hint}
        </p>
      )}
    </div>
  );
}

function resolveWeightLabel(
  weight: number,
  labels: BinaryWinPanelLabels
): { readonly text: string; readonly tone: 'win' | 'lose' | 'neutral' } {
  if (weight > 0) return { text: labels.weightWinLabel, tone: 'win' };
  if (weight < 0) return { text: labels.weightLoseLabel, tone: 'lose' };
  return { text: labels.weightNeutralLabel, tone: 'neutral' };
}

const WEIGHT_BADGE_CLASSES: Readonly<Record<'win' | 'lose' | 'neutral', string>> = {
  win: 'bg-entity-toolkit/15 text-entity-toolkit',
  lose: 'bg-entity-event/15 text-entity-event',
  neutral: 'bg-muted text-muted-foreground',
};

export function BinaryWinPanel({ data, labels, className }: BinaryWinPanelProps): ReactElement {
  const { collective } = data;
  const hasCategories = data.categories.length > 0;

  const goalAria = labels.meterAriaLabelTemplate
    .replace('{value}', String(collective.goalValue))
    .replace('{max}', String(collective.goalMax));
  const failAria = labels.meterAriaLabelTemplate
    .replace('{value}', String(collective.failValue))
    .replace('{max}', String(collective.failMax));

  return (
    <section
      data-testid="scoring-panel-binarywin"
      data-score-type="BinaryWin"
      aria-label={labels.title}
      className={`flex flex-col gap-3 rounded-lg border border-border bg-card p-3 ${className ?? ''}`}
    >
      <h3 className="text-sm font-semibold text-foreground">{labels.title}</h3>

      <div className="flex flex-col gap-2">
        <Meter
          variant="goal"
          label={collective.goalLabel}
          value={collective.goalValue}
          max={collective.goalMax}
          hint={collective.goalHint}
          ariaLabel={goalAria}
        />
        <Meter
          variant="fail"
          label={collective.failLabel}
          value={collective.failValue}
          max={collective.failMax}
          hint={collective.failHint}
          ariaLabel={failAria}
        />
      </div>

      {hasCategories && (
        <div className="flex flex-col gap-2 border-t border-border pt-2">
          <h4 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            {labels.categoriesTitle}
          </h4>
          <ul role="list" className="flex flex-col gap-1">
            {data.categories.map(cat => {
              const weight = resolveWeightLabel(cat.weight, labels);
              return (
                <li
                  key={cat.id}
                  className="flex items-center justify-between gap-2 rounded px-2 py-1
                    text-xs"
                >
                  <div className="flex min-w-0 items-center gap-2">
                    <span className="truncate text-foreground">{cat.label}</span>
                    <span
                      className="shrink-0 rounded-full bg-muted px-1.5 py-0.5
                        font-mono text-[10px] uppercase tracking-wider text-muted-foreground"
                    >
                      {cat.computation}
                    </span>
                  </div>
                  <span
                    className={[
                      'shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-semibold',
                      WEIGHT_BADGE_CLASSES[weight.tone],
                    ].join(' ')}
                  >
                    {weight.text}
                  </span>
                </li>
              );
            })}
          </ul>
        </div>
      )}
    </section>
  );
}
