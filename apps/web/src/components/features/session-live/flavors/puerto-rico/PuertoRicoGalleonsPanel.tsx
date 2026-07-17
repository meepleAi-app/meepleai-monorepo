'use client';

import { type ReactElement } from 'react';

import { puertoRicoGoodColor } from './puerto-rico-palette';
import {
  PUERTO_RICO_GOODS,
  type PuertoRicoGalleon,
  type PuertoRicoGood,
} from './puerto-rico-state';

export interface PuertoRicoGalleonsPanelProps {
  readonly galleons: PuertoRicoGalleon[];
  readonly editable: boolean;
  readonly onSetGood?: (index: number, good: PuertoRicoGood | null) => void;
  readonly onBumpLoaded?: (index: number, delta: 1 | -1) => void;
  readonly labels: {
    heading: string;
    emptyGood: string;
    loadedAria: string;
    unloadAria: string;
    capTemplate: string;
  };
}

export function PuertoRicoGalleonsPanel({
  galleons,
  editable,
  onSetGood,
  onBumpLoaded,
  labels,
}: PuertoRicoGalleonsPanelProps): ReactElement {
  return (
    <section data-slot="pr-galleons" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>
      <ul role="list" className="flex flex-col gap-1">
        {galleons.map((g, i) => (
          <li
            key={i}
            data-slot="pr-galleon"
            data-index={String(i)}
            className="flex items-center gap-2 rounded-lg border border-border bg-card px-2 py-1"
          >
            {g.good != null ? (
              <span
                aria-hidden="true"
                className="h-3 w-3 rounded-full"
                style={{ backgroundColor: puertoRicoGoodColor(g.good) }}
              />
            ) : (
              <span aria-hidden="true" className="text-xs text-muted-foreground">
                {labels.emptyGood}
              </span>
            )}
            {editable ? (
              <select
                value={g.good ?? ''}
                onChange={e =>
                  onSetGood?.(i, e.target.value === '' ? null : (e.target.value as PuertoRicoGood))
                }
                className="rounded border border-border bg-background px-1 py-0.5 text-xs text-foreground"
              >
                <option value="">{labels.emptyGood}</option>
                {PUERTO_RICO_GOODS.map(good => (
                  <option key={good} value={good}>
                    {good}
                  </option>
                ))}
              </select>
            ) : (
              <span className="text-xs text-foreground">{g.good ?? labels.emptyGood}</span>
            )}
            <span className="ml-auto tabular-nums text-sm font-bold text-foreground">
              {labels.capTemplate
                .replace('{loaded}', String(g.loaded))
                .replace('{cap}', String(g.cap))}
            </span>
            {editable && (
              <span className="flex items-center gap-1">
                <button
                  type="button"
                  data-dir="dec"
                  aria-label={labels.unloadAria.replace('{n}', String(i + 1))}
                  onClick={() => onBumpLoaded?.(i, -1)}
                  className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
                >
                  −
                </button>
                <button
                  type="button"
                  data-dir="inc"
                  aria-label={labels.loadedAria.replace('{n}', String(i + 1))}
                  onClick={() => onBumpLoaded?.(i, 1)}
                  className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
                >
                  +
                </button>
              </span>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
