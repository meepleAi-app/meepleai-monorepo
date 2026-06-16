'use client';

/**
 * FirstPlayerTokenBranch — Issue #2378 G5b.
 *
 * Renders first-player-token turn order: shows who holds the token,
 * current round, and play order with the token holder highlighted.
 */

import type { ReactElement } from 'react';

import type { PlayerInfo, TurnState } from '@/lib/session-live/turn-state';

import type { TurnIndicatorRendererLabels } from '../labels';

interface Props {
  readonly state: Extract<TurnState, { type: 'FirstPlayerToken' }>;
  readonly players: ReadonlyArray<PlayerInfo>;
  readonly viewerId: string;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorRendererLabels;
}

export function FirstPlayerTokenBranch({
  state,
  players,
  viewerId,
  compact = false,
  labels,
}: Props): ReactElement {
  const tokenHolder = players.find(p => p.id === state.tokenHolderId);
  const tokenName = tokenHolder?.name ?? 'Sconosciuto';
  const tokenLabel = labels.firstPlayerTokenHolderTemplate.replace('{playerName}', tokenName);
  const hasToken = state.tokenHolderId === viewerId;

  return (
    <section
      data-slot="turn-branch-first-player-token"
      role="region"
      aria-label={labels.firstPlayerTokenHeading}
      className={`flex flex-col ${compact ? 'gap-2' : 'gap-3'}`}
    >
      <h4 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
        {labels.firstPlayerTokenHeading}
      </h4>
      <div
        aria-live="polite"
        className="flex items-center gap-3 rounded-lg border border-entity-player/30 bg-entity-player/10 p-3"
      >
        <span aria-hidden="true" className="text-2xl">
          ⭐
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-xs font-semibold uppercase tracking-wider text-entity-player">
            Token primo giocatore
          </p>
          <p
            className={`truncate text-sm font-semibold ${
              hasToken ? 'text-entity-player' : 'text-foreground'
            }`}
          >
            {tokenLabel}
          </p>
        </div>
      </div>
      <p className="text-xs text-muted-foreground">
        Round {state.round} / {state.totalRounds}
      </p>
      <div>
        <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-2">
          {labels.playOrderHeading}
        </p>
        <ol className="flex flex-wrap gap-2">
          {state.playOrder.map(id => {
            const p = players.find(q => q.id === id);
            const isToken = id === state.tokenHolderId;
            return (
              <li
                key={id}
                data-token={isToken ? 'true' : undefined}
                className={`rounded-full border px-3 py-1 text-xs font-medium ${
                  isToken
                    ? 'border-entity-player/40 bg-entity-player/10 text-entity-player'
                    : 'border-border bg-card text-muted-foreground'
                }`}
              >
                {isToken && <span aria-hidden="true">⭐ </span>}
                {p?.name ?? id}
              </li>
            );
          })}
        </ol>
      </div>
    </section>
  );
}
