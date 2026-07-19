'use client';

import { type ReactElement } from 'react';

import { WINGSPAN_ROUND_TURN_BUDGET, type WingspanGameState } from './wingspan-state';

export interface WingspanRoundTrackerLabels {
  readonly heading: string;
  readonly roundTemplate: string; // "Round {n}/4"
  readonly turnBudgetTemplate: string; // "{n} turni"
  readonly goalsHeading: string;
  readonly goalPlaceholderTemplate: string; // "Obiettivo round {n}"
  readonly advanceRoundLabel: string;
}

export interface WingspanRoundTrackerProps {
  readonly state: WingspanGameState;
  readonly editable: boolean;
  readonly onAdvanceRound?: () => void;
  readonly onSetRoundGoal?: (index: number, label: string) => void;
  readonly labels: WingspanRoundTrackerLabels;
}

export function WingspanRoundTracker({
  state,
  editable,
  onAdvanceRound,
  onSetRoundGoal,
  labels,
}: WingspanRoundTrackerProps): ReactElement {
  const budget = WINGSPAN_ROUND_TURN_BUDGET[Math.min(Math.max(state.round, 1), 4) - 1];
  // Always render 4 goal slots for the host; read-only shows only the entered ones.
  const slots = editable ? 4 : state.roundGoals.length;

  return (
    <section
      data-slot="wingspan-round-tracker"
      className="flex flex-col gap-2 rounded-lg border border-border bg-card p-3"
    >
      <div className="flex items-center gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {labels.heading}
        </h3>
        <span data-slot="wingspan-round" className="text-sm font-bold text-foreground">
          {labels.roundTemplate.replace('{n}', String(state.round))}
        </span>
        <span className="text-xs text-muted-foreground">
          {labels.turnBudgetTemplate.replace('{n}', String(budget))}
        </span>
        {editable && (
          <button
            type="button"
            onClick={() => onAdvanceRound?.()}
            className="ml-auto rounded-md border border-border bg-background px-2 py-1 text-xs font-semibold text-foreground hover:bg-muted"
          >
            {labels.advanceRoundLabel}
          </button>
        )}
      </div>

      <div data-slot="wingspan-goals" className="flex flex-col gap-1">
        <span className="text-[10px] uppercase tracking-wide text-muted-foreground">
          {labels.goalsHeading}
        </span>
        {Array.from({ length: slots }, (_, i) => {
          const label = state.roundGoals[i]?.label ?? '';
          const placeholder = labels.goalPlaceholderTemplate.replace('{n}', String(i + 1));
          return editable ? (
            <input
              key={i}
              type="text"
              aria-label={placeholder}
              placeholder={placeholder}
              value={label}
              onChange={e => onSetRoundGoal?.(i, e.target.value)}
              className="rounded-md border border-border bg-background px-2 py-1 text-xs text-foreground"
            />
          ) : (
            <span key={i} className="rounded bg-muted px-2 py-1 text-xs text-foreground">
              {label || placeholder}
            </span>
          );
        })}
      </div>
    </section>
  );
}
