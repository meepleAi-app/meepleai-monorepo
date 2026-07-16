'use client';

import { type ReactElement } from 'react';

import type { CatanGameState } from './catan-state';

const ROLLS = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

export interface CatanDiceControlProps {
  readonly dice: CatanGameState['dice'];
  readonly editable: boolean;
  readonly onRoll?: (sum: number) => void;
  readonly lastLabel: string;
  readonly historyLabel: string;
  readonly rollAriaTemplate: string;
}

export function CatanDiceControl({
  dice,
  editable,
  onRoll,
  lastLabel,
  historyLabel,
  rollAriaTemplate,
}: CatanDiceControlProps): ReactElement {
  return (
    <div
      data-slot="catan-dice"
      className="flex flex-col gap-2 rounded-lg border border-border bg-card p-3"
    >
      <div className="flex items-center gap-2">
        <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {lastLabel}
        </span>
        <span
          data-slot="catan-dice-last"
          className="text-2xl font-bold tabular-nums text-foreground"
        >
          {dice.last ?? '—'}
        </span>
      </div>

      {dice.history.length > 0 && (
        <div className="flex flex-wrap items-center gap-1">
          <span className="text-[10px] uppercase tracking-wide text-muted-foreground">
            {historyLabel}
          </span>
          {dice.history.slice(0, 12).map((n, i) => (
            <span
              key={`${i}-${n}`}
              className="rounded bg-muted px-1.5 py-0.5 text-[11px] tabular-nums text-muted-foreground"
            >
              {n}
            </span>
          ))}
        </div>
      )}

      {editable && (
        <div className="grid grid-cols-6 gap-1" data-slot="catan-dice-pad">
          {ROLLS.map(n => (
            <button
              key={n}
              type="button"
              aria-label={rollAriaTemplate.replace('{n}', String(n))}
              onClick={() => onRoll?.(n)}
              className="rounded-md border border-border bg-background py-1 text-sm font-semibold tabular-nums text-foreground hover:bg-muted"
            >
              {n}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
