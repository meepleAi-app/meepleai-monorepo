/**
 * GameNightLocationToggle — Step 2 ("Dove") component.
 * Issue #950 W3 Components. Spec §5 (C3).
 *
 * 4-option segmented control + details textarea (hidden when kind=tbd).
 * Pure component: orchestrator owns state, this only emits intent.
 */

'use client';

import { useId, type ReactElement } from 'react';

import type { LocationKind } from '@/lib/game-nights/wizard-types';

export interface GameNightLocationToggleLabels {
  readonly label: string;
  readonly kindHome: string;
  readonly kindFriend: string;
  readonly kindOnline: string;
  readonly kindTbd: string;
  readonly detailsLabel: string;
  readonly detailsPlaceholder: string;
  readonly detailsHelper: string;
}

export interface GameNightLocationToggleProps {
  readonly kind: LocationKind;
  readonly details: string;
  readonly onSetLocation: (kind: LocationKind, details: string) => void;
  readonly labels: GameNightLocationToggleLabels;
  /** Maximum allowed characters for details (mirror BE = 500). */
  readonly maxDetailsLength?: number;
}

const OPTIONS: readonly LocationKind[] = ['home', 'friend', 'online', 'tbd'];

function kindLabel(kind: LocationKind, labels: GameNightLocationToggleLabels): string {
  switch (kind) {
    case 'home':
      return labels.kindHome;
    case 'friend':
      return labels.kindFriend;
    case 'online':
      return labels.kindOnline;
    case 'tbd':
      return labels.kindTbd;
  }
}

export function GameNightLocationToggle({
  kind,
  details,
  onSetLocation,
  labels,
  maxDetailsLength = 500,
}: GameNightLocationToggleProps): ReactElement {
  const groupId = useId();
  const detailsId = useId();
  const showDetails = kind !== 'tbd';

  return (
    <section
      data-slot="game-night-create-step2"
      aria-labelledby={`${groupId}-label`}
      className="flex flex-col gap-4"
    >
      <p id={`${groupId}-label`} className="text-sm font-medium text-foreground">
        {labels.label}
      </p>

      <div
        role="radiogroup"
        aria-labelledby={`${groupId}-label`}
        className="flex flex-wrap gap-2"
        data-slot="game-night-create-step2-kinds"
      >
        {OPTIONS.map(option => {
          const selected = option === kind;
          return (
            <button
              key={option}
              type="button"
              role="radio"
              aria-checked={selected}
              onClick={() => onSetLocation(option, option === 'tbd' ? '' : details)}
              data-slot={`game-night-create-step2-kind-${option}`}
              className={
                selected
                  ? 'rounded-md border border-primary bg-primary/10 px-3 py-2 text-sm font-medium text-foreground'
                  : 'rounded-md border border-border bg-card px-3 py-2 text-sm text-muted-foreground hover:text-foreground'
              }
            >
              {kindLabel(option, labels)}
            </button>
          );
        })}
      </div>

      {showDetails && (
        <div className="flex flex-col gap-2">
          <label htmlFor={detailsId} className="text-sm font-medium text-foreground">
            {labels.detailsLabel}
          </label>
          <textarea
            id={detailsId}
            value={details}
            onChange={e => onSetLocation(kind, e.target.value.slice(0, maxDetailsLength))}
            maxLength={maxDetailsLength}
            placeholder={labels.detailsPlaceholder}
            rows={3}
            className="rounded-md border border-border bg-card px-3 py-2 text-sm text-foreground"
            data-slot="game-night-create-step2-details"
          />
          <p className="text-xs text-muted-foreground">{labels.detailsHelper}</p>
        </div>
      )}
    </section>
  );
}
