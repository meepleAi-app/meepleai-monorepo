'use client';

import { type ReactElement } from 'react';

import { POWER_GRID_PLANT_BANKS, type PowerGridPlantBank } from './power-grid-state';

export interface PowerGridPlantMarketPanelProps {
  readonly plants: { current: (number | null)[]; future: (number | null)[] };
  readonly editable: boolean;
  readonly onSetPlant?: (bank: PowerGridPlantBank, index: number, plant: number | null) => void;
  readonly labels: {
    heading: string;
    currentBank: string;
    futureBank: string;
    emptySlot: string;
    slotAria: string;
  };
}

export function PowerGridPlantMarketPanel({
  plants,
  editable,
  onSetPlant,
  labels,
}: PowerGridPlantMarketPanelProps): ReactElement {
  const bankLabel = (bank: PowerGridPlantBank): string =>
    bank === 'current' ? labels.currentBank : labels.futureBank;

  const parseInput = (raw: string): number | null => {
    const trimmed = raw.trim();
    if (trimmed === '') return null;
    const n = Number.parseInt(trimmed, 10);
    return Number.isNaN(n) ? null : n;
  };

  return (
    <section data-slot="pg-plants" className="flex flex-col gap-2">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>
      {POWER_GRID_PLANT_BANKS.map(bank => (
        <div key={bank} data-slot="pg-plant-bank" data-bank={bank} className="flex flex-col gap-1">
          <span className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
            {bankLabel(bank)}
          </span>
          <div className="grid grid-cols-4 gap-1">
            {plants[bank].map((plant, i) => (
              <div
                key={i}
                data-slot="pg-plant-slot"
                data-bank={bank}
                data-index={String(i)}
                className="flex items-center justify-center rounded-lg border border-border bg-card p-1"
              >
                {editable ? (
                  <input
                    type="number"
                    min={0}
                    inputMode="numeric"
                    aria-label={labels.slotAria
                      .replace('{bank}', bankLabel(bank))
                      .replace('{n}', String(i + 1))}
                    defaultValue={plant ?? ''}
                    onChange={e => onSetPlant?.(bank, i, parseInput(e.target.value))}
                    className="w-full bg-transparent text-center text-sm font-semibold tabular-nums text-foreground outline-none"
                  />
                ) : (
                  <span className="text-sm font-semibold tabular-nums text-foreground">
                    {plant ?? labels.emptySlot}
                  </span>
                )}
              </div>
            ))}
          </div>
        </div>
      ))}
    </section>
  );
}
