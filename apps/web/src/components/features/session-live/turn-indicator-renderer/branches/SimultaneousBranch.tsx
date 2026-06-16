'use client';

/**
 * SimultaneousBranch — Issue #2378 G5b.
 *
 * Renders simultaneous turn order: player grid showing all players
 * act at once, with optional active phase strip.
 */

import type { ReactElement } from 'react';

import type { PlayerInfo, TurnState } from '@/lib/session-live/turn-state';

import type { TurnIndicatorRendererLabels } from '../labels';

interface Props {
  readonly state: Extract<TurnState, { type: 'Simultaneous' }>;
  readonly players: ReadonlyArray<PlayerInfo>;
  readonly viewerId: string;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorRendererLabels;
}

export function SimultaneousBranch({
  state,
  players,
  compact = false,
  labels,
}: Props): ReactElement {
  return (
    <section
      data-slot="turn-branch-simultaneous"
      role="region"
      aria-label={labels.simultaneousHeading}
      className={`flex flex-col ${compact ? 'gap-2' : 'gap-3'}`}
    >
      <h4 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
        {labels.simultaneousHeading}
      </h4>
      <div
        role="status"
        className="rounded-lg border border-entity-toolkit/30 bg-entity-toolkit/10 p-3 text-center"
      >
        <p className="text-sm font-semibold text-entity-toolkit">
          <span aria-hidden="true">⚡ </span>Tutti giocano insieme
        </p>
      </div>
      <ul className="grid grid-cols-2 gap-2">
        {players.map(p => (
          <li
            key={p.id}
            className="flex items-center gap-2 rounded-md border border-entity-toolkit/30 bg-entity-toolkit/10 px-3 py-2"
          >
            <span className="text-sm font-semibold text-foreground">{p.name}</span>
          </li>
        ))}
      </ul>
      {state.phases && state.phases.length > 0 && state.activePhaseIndex !== undefined && (
        <div aria-live="polite" className="flex gap-1.5">
          {state.phases.map((phase, i) => {
            const active = i === state.activePhaseIndex;
            return (
              <div
                key={phase}
                aria-current={active ? 'step' : undefined}
                className={`flex-1 rounded-md border px-2 py-2 text-center text-xs font-bold ${
                  active
                    ? 'border-entity-player/40 bg-entity-player/10 text-entity-player'
                    : 'border-border bg-card text-muted-foreground'
                }`}
              >
                {phase}
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}
