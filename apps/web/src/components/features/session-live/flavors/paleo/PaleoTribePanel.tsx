'use client';

import { type ReactElement } from 'react';

import type { LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import { paleoStatusColor } from './paleo-palette';

import type { PaleoStatus } from './paleo-state';

export interface PaleoTribePanelProps {
  readonly players: LiveSessionPlayerDto[];
  readonly survivors: Record<string, PaleoStatus>;
  readonly editable: boolean;
  readonly onCycle?: (playerId: string) => void;
  readonly labels: {
    heading: string;
    statusAlive: string;
    statusWounded: string;
    statusDead: string;
    cycleAria: string;
  };
}

export function PaleoTribePanel({
  players,
  survivors,
  editable,
  onCycle,
  labels,
}: PaleoTribePanelProps): ReactElement {
  const statusLabel = (s: PaleoStatus): string =>
    s === 'alive' ? labels.statusAlive : s === 'wounded' ? labels.statusWounded : labels.statusDead;

  return (
    <section data-slot="paleo-tribe" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>
      <ul role="list" className="flex flex-col gap-1">
        {players.map(player => {
          const status = survivors[player.id] ?? 'alive';
          const badge = (
            <span
              className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold"
              style={{ backgroundColor: paleoStatusColor(status), color: 'hsl(0, 0%, 100%)' }}
            >
              {statusLabel(status)}
            </span>
          );
          return (
            <li
              key={player.id}
              data-slot="paleo-tribe-row"
              data-status={status}
              className="flex items-center gap-2 rounded-lg border border-border bg-card px-2 py-1"
            >
              <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                {player.displayName}
              </span>
              {editable ? (
                <button
                  type="button"
                  aria-label={labels.cycleAria.replace('{name}', player.displayName)}
                  onClick={() => onCycle?.(player.id)}
                  className="rounded hover:opacity-80"
                >
                  {badge}
                </button>
              ) : (
                badge
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}
