'use client';

import { type ReactElement } from 'react';

import type { LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import { zombicideWoundColor } from './zombicide-palette';

import type { WoundLevel } from './zombicide-state';

export interface ZombicideSurvivorsPanelProps {
  readonly players: LiveSessionPlayerDto[];
  readonly survivors: Record<string, WoundLevel>;
  readonly editable: boolean;
  readonly onCycle?: (playerId: string) => void;
  readonly labels: {
    heading: string;
    healthy: string;
    wounded: string;
    down: string;
    cycleAria: string;
  };
}

export function ZombicideSurvivorsPanel({
  players,
  survivors,
  editable,
  onCycle,
  labels,
}: ZombicideSurvivorsPanelProps): ReactElement {
  const woundLabel = (w: WoundLevel): string =>
    w === 0 ? labels.healthy : w === 1 ? labels.wounded : labels.down;

  return (
    <section data-slot="zc-survivors" className="flex flex-col gap-1">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {labels.heading}
      </h3>
      <ul role="list" className="flex flex-col gap-1">
        {players.map(player => {
          const wounds = survivors[player.id] ?? 0;
          const isDown = wounds === 2;
          const badge = (
            <span
              className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold"
              style={{ backgroundColor: zombicideWoundColor(wounds), color: 'hsl(0, 0%, 100%)' }}
            >
              {woundLabel(wounds)}
            </span>
          );
          return (
            <li
              key={player.id}
              data-slot="zc-survivor-row"
              data-wounds={String(wounds)}
              data-down={String(isDown)}
              className="flex items-center gap-2 rounded-lg border border-border bg-card px-2 py-1"
            >
              <span
                className={`min-w-0 flex-1 truncate text-xs font-medium ${isDown ? 'text-muted-foreground line-through' : 'text-foreground'}`}
              >
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
