'use client';

import { type ReactElement } from 'react';

import type { LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import { puertoRicoGoodColor } from './puerto-rico-palette';
import {
  PUERTO_RICO_GOODS,
  type PuertoRicoGood,
  type PuertoRicoPlayerState,
} from './puerto-rico-state';

type PlayerCounter = 'doubloons' | 'colonists' | 'plantations' | 'quarries' | 'buildings';

export interface PuertoRicoPlayerMatSummaryProps {
  readonly player: LiveSessionPlayerDto;
  readonly state: PuertoRicoPlayerState;
  readonly editable: boolean;
  readonly onBumpCounter?: (field: PlayerCounter, delta: 1 | -1) => void;
  readonly onBumpGood?: (good: PuertoRicoGood, delta: 1 | -1) => void;
  readonly labels: {
    doubloonsLabel: string;
    colonistsLabel: string;
    plantationsLabel: string;
    quarriesLabel: string;
    buildingsLabel: string;
    incAria: string;
    decAria: string;
  };
}

function Stepper({
  label,
  value,
  editable,
  incAria,
  decAria,
  onDelta,
  data,
}: {
  label: string;
  value: number;
  editable: boolean;
  incAria: string;
  decAria: string;
  onDelta?: (d: 1 | -1) => void;
  data?: Record<string, string>;
}): ReactElement {
  return (
    <span className="inline-flex items-center gap-1 text-xs" {...data}>
      <span className="text-muted-foreground">{label}</span>
      {editable && (
        <button
          type="button"
          data-dir="dec"
          aria-label={decAria}
          onClick={() => onDelta?.(-1)}
          className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
        >
          −
        </button>
      )}
      <span className="min-w-4 text-center font-semibold tabular-nums text-foreground">
        {value}
      </span>
      {editable && (
        <button
          type="button"
          data-dir="inc"
          aria-label={incAria}
          onClick={() => onDelta?.(1)}
          className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
        >
          +
        </button>
      )}
    </span>
  );
}

export function PuertoRicoPlayerMatSummary({
  player,
  state,
  editable,
  onBumpCounter,
  onBumpGood,
  labels,
}: PuertoRicoPlayerMatSummaryProps): ReactElement {
  const inc = (f: string) => labels.incAria.replace('{field}', f);
  const dec = (f: string) => labels.decAria.replace('{field}', f);
  const counters: Array<[PlayerCounter, string]> = [
    ['doubloons', labels.doubloonsLabel],
    ['colonists', labels.colonistsLabel],
    ['plantations', labels.plantationsLabel],
    ['quarries', labels.quarriesLabel],
    ['buildings', labels.buildingsLabel],
  ];

  return (
    <div
      data-slot="pr-player-mat"
      className="flex flex-col gap-1.5 rounded-lg border border-border bg-card p-2"
    >
      <span className="text-xs font-semibold text-foreground">{player.displayName}</span>

      <div className="flex flex-wrap gap-x-3 gap-y-1">
        {counters.map(([field, label]) => (
          <Stepper
            key={field}
            label={label}
            value={state[field]}
            editable={editable}
            incAria={inc(label)}
            decAria={dec(label)}
            onDelta={d => onBumpCounter?.(field, d)}
          />
        ))}
      </div>

      <div data-slot="pr-storehouse" className="flex flex-wrap gap-2">
        {PUERTO_RICO_GOODS.map(good => (
          <span
            key={good}
            data-good={good}
            className="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs"
            style={{
              backgroundColor: puertoRicoGoodColor(good),
              color: good === 'sugar' || good === 'corn' ? 'hsl(0,0%,15%)' : 'hsl(0,0%,100%)',
            }}
          >
            <span className="font-semibold tabular-nums">{state.storehouse[good]}</span>
            {editable && (
              <>
                <button
                  type="button"
                  data-dir="dec"
                  aria-label={dec(good)}
                  onClick={() => onBumpGood?.(good, -1)}
                  className="h-4 w-4 rounded border border-border leading-none"
                >
                  −
                </button>
                <button
                  type="button"
                  data-dir="inc"
                  aria-label={inc(good)}
                  onClick={() => onBumpGood?.(good, 1)}
                  className="h-4 w-4 rounded border border-border leading-none"
                >
                  +
                </button>
              </>
            )}
          </span>
        ))}
      </div>
    </div>
  );
}
