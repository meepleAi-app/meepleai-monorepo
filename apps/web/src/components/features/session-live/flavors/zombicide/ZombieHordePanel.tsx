'use client';

import { type ReactElement } from 'react';

import { ZOMBIE_TYPES, type ZombieCounts, type ZombieType } from './zombicide-state';

export interface ZombieHordePanelProps {
  readonly zombies: ZombieCounts;
  readonly editable: boolean;
  readonly onBump?: (type: ZombieType, delta: 1 | -1) => void;
  readonly labels: {
    heading: string;
    walker: string;
    runner: string;
    fatty: string;
    berserker: string;
    abomination: string;
    necromancer: string;
    incAria: string;
    decAria: string;
  };
}

export function ZombieHordePanel({
  zombies,
  editable,
  onBump,
  labels,
}: ZombieHordePanelProps): ReactElement {
  const inc = (f: string) => labels.incAria.replace('{field}', f);
  const dec = (f: string) => labels.decAria.replace('{field}', f);
  const typeLabel: Record<ZombieType, string> = {
    walker: labels.walker,
    runner: labels.runner,
    fatty: labels.fatty,
    berserker: labels.berserker,
    abomination: labels.abomination,
    necromancer: labels.necromancer,
  };

  return (
    <section data-slot="zc-horde" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>
      <div className="grid grid-cols-2 gap-2">
        {ZOMBIE_TYPES.map(type => {
          const label = typeLabel[type];
          return (
            <div
              key={type}
              data-zombie={type}
              className="flex items-center gap-2 rounded-lg border border-border bg-card px-2 py-1 text-xs"
            >
              <span className="min-w-0 flex-1 truncate text-muted-foreground">{label}</span>
              {editable && (
                <button
                  type="button"
                  data-dir="dec"
                  aria-label={dec(label)}
                  onClick={() => onBump?.(type, -1)}
                  className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
                >
                  −
                </button>
              )}
              <span className="min-w-4 text-center font-semibold tabular-nums text-foreground">
                {zombies[type]}
              </span>
              {editable && (
                <button
                  type="button"
                  data-dir="inc"
                  aria-label={inc(label)}
                  onClick={() => onBump?.(type, 1)}
                  className="h-5 w-5 rounded border border-border text-foreground hover:bg-muted"
                >
                  +
                </button>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
}
