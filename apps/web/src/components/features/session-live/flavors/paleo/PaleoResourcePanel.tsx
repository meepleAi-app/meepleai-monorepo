'use client';

import { type ReactElement } from 'react';

import { type PaleoResource, type PaleoResources } from './paleo-state';

export interface PaleoResourcePanelProps {
  readonly resources: PaleoResources;
  readonly editable: boolean;
  readonly onBump?: (field: PaleoResource, delta: 1 | -1) => void;
  readonly labels: {
    heading: string;
    wood: string;
    stone: string;
    food: string;
    knowledge: string;
    incAria: string;
    decAria: string;
  };
}

export function PaleoResourcePanel({
  resources,
  editable,
  onBump,
  labels,
}: PaleoResourcePanelProps): ReactElement {
  const inc = (f: string) => labels.incAria.replace('{field}', f);
  const dec = (f: string) => labels.decAria.replace('{field}', f);
  const rows: Array<[PaleoResource, string]> = [
    ['wood', labels.wood],
    ['stone', labels.stone],
    ['food', labels.food],
    ['knowledge', labels.knowledge],
  ];

  return (
    <section data-slot="paleo-resources" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>
      <div className="grid grid-cols-2 gap-2">
        {rows.map(([field, label]) => (
          <div
            key={field}
            data-resource={field}
            className="flex items-center gap-2 rounded-lg border border-border bg-card px-2 py-1 text-xs"
          >
            <span className="min-w-0 flex-1 truncate text-muted-foreground">{label}</span>
            {editable && (
              <button
                type="button"
                data-dir="dec"
                aria-label={dec(label)}
                onClick={() => onBump?.(field, -1)}
                className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
              >
                −
              </button>
            )}
            <span className="min-w-4 text-center font-semibold tabular-nums text-foreground">
              {resources[field]}
            </span>
            {editable && (
              <button
                type="button"
                data-dir="inc"
                aria-label={inc(label)}
                onClick={() => onBump?.(field, 1)}
                className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
              >
                +
              </button>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}
