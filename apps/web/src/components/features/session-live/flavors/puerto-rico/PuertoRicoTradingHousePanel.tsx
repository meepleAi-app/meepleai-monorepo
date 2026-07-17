'use client';

import { type ReactElement } from 'react';

import { puertoRicoGoodColor } from './puerto-rico-palette';
import { PUERTO_RICO_GOODS, type PuertoRicoGood } from './puerto-rico-state';

export interface PuertoRicoTradingHousePanelProps {
  readonly slots: (PuertoRicoGood | null)[];
  readonly editable: boolean;
  readonly onSetSlot?: (index: number, good: PuertoRicoGood | null) => void;
  readonly labels: { heading: string; emptyGood: string; slotAria: string };
}

export function PuertoRicoTradingHousePanel({
  slots,
  editable,
  onSetSlot,
  labels,
}: PuertoRicoTradingHousePanelProps): ReactElement {
  return (
    <section data-slot="pr-trading" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>
      <div className="flex gap-1">
        {slots.map((good, i) => (
          <div
            key={i}
            data-slot="pr-trade-slot"
            data-index={String(i)}
            className="flex flex-1 flex-col items-center gap-1 rounded-lg border border-border bg-card p-1"
          >
            <span
              aria-hidden="true"
              className="h-3 w-3 rounded-full"
              style={{
                backgroundColor: good != null ? puertoRicoGoodColor(good) : 'transparent',
                borderWidth: good == null ? 1 : 0,
              }}
            />
            {editable ? (
              <select
                aria-label={labels.slotAria.replace('{n}', String(i + 1))}
                value={good ?? ''}
                onChange={e =>
                  onSetSlot?.(i, e.target.value === '' ? null : (e.target.value as PuertoRicoGood))
                }
                className="w-full rounded border border-border bg-background px-0.5 py-0.5 text-[10px] text-foreground"
              >
                <option value="">{labels.emptyGood}</option>
                {PUERTO_RICO_GOODS.map(g => (
                  <option key={g} value={g}>
                    {g}
                  </option>
                ))}
              </select>
            ) : (
              <span className="text-[10px] text-foreground">{good ?? labels.emptyGood}</span>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}
