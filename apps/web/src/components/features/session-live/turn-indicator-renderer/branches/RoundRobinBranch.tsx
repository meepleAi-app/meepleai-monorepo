'use client';

/**
 * RoundRobinBranch — Issue #2378 G5b.
 *
 * Renders round-robin turn order: progress bar via existing TurnIndicator
 * + play-order strip highlighting the active player.
 */

import type { ReactElement } from 'react';

import { TurnIndicator } from '@/components/features/session-live/TurnIndicator';
import type { PlayerInfo, TurnState } from '@/lib/session-live/turn-state';

import type { TurnIndicatorRendererLabels } from '../labels';

interface Props {
  readonly state: Extract<TurnState, { type: 'RoundRobin' }>;
  readonly players: ReadonlyArray<PlayerInfo>;
  readonly viewerId: string;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorRendererLabels;
}

export function RoundRobinBranch({
  state,
  players,
  viewerId,
  compact = false,
  labels,
}: Props): ReactElement {
  const activePlayer = players.find(p => p.id === state.activePlayerId);
  const activeName = activePlayer?.name ?? 'Sconosciuto';
  const isMyTurn = state.activePlayerId === viewerId;

  return (
    <section
      data-slot="turn-branch-round-robin"
      role="region"
      aria-label={labels.roundRobinHeading}
      className="flex flex-col gap-3"
    >
      <h4 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
        {labels.roundRobinHeading}
      </h4>
      <div aria-live="polite">
        <TurnIndicator
          current={state.round}
          total={state.totalRounds}
          activePlayerName={activeName}
          isMyTurn={isMyTurn}
          compact={compact}
          labels={{
            currentTurnAriaLabel: labels.roundCountTemplate,
            activePlayerLabel: '{playerName}',
            yourTurnLabel: labels.yourTurnLabel,
            waitingLabel: labels.waitingLabel,
          }}
        />
      </div>
      <div>
        <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-2">
          {labels.playOrderHeading}
        </p>
        <ol className="flex flex-wrap gap-2">
          {state.playOrder.map(id => {
            const p = players.find(q => q.id === id);
            const isActive = id === state.activePlayerId;
            return (
              <li
                key={id}
                data-active={isActive ? 'true' : undefined}
                className={`rounded-full border px-3 py-1 text-xs font-medium ${
                  isActive
                    ? 'border-entity-player/40 bg-entity-player/10 text-entity-player'
                    : 'border-border bg-card text-muted-foreground'
                }`}
              >
                {p?.name ?? id}
              </li>
            );
          })}
        </ol>
      </div>
    </section>
  );
}
