'use client';

import { type ReactElement } from 'react';

export interface PuertoRicoColonistShipPanelProps {
  readonly colonistShip: { onShip: number; supply: number };
  readonly editable: boolean;
  readonly onBump?: (field: 'onShip' | 'supply', delta: 1 | -1) => void;
  readonly labels: {
    heading: string;
    onShipLabel: string;
    supplyLabel: string;
    incAria: string;
    decAria: string;
  };
}

export function PuertoRicoColonistShipPanel({
  colonistShip,
  editable,
  onBump,
  labels,
}: PuertoRicoColonistShipPanelProps): ReactElement {
  const rows: Array<['onShip' | 'supply', string]> = [
    ['onShip', labels.onShipLabel],
    ['supply', labels.supplyLabel],
  ];
  return (
    <section
      data-slot="pr-colonist-ship"
      className="flex flex-col gap-1 rounded-lg border border-border bg-card p-2"
    >
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>
      {rows.map(([field, label]) => (
        <div key={field} className="flex items-center gap-2 text-xs">
          <span className="text-muted-foreground">{label}</span>
          {editable && (
            <button
              type="button"
              aria-label={labels.decAria.replace('{field}', label)}
              onClick={() => onBump?.(field, -1)}
              className="ml-auto h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
            >
              −
            </button>
          )}
          <span
            className={`${editable ? '' : 'ml-auto'} min-w-4 text-center font-semibold tabular-nums text-foreground`}
          >
            {colonistShip[field]}
          </span>
          {editable && (
            <button
              type="button"
              aria-label={labels.incAria.replace('{field}', label)}
              onClick={() => onBump?.(field, 1)}
              className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
            >
              +
            </button>
          )}
        </div>
      ))}
    </section>
  );
}
