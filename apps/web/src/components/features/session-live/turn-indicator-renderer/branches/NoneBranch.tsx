'use client';

/**
 * NoneBranch — Issue #2378 G5b.
 *
 * Renders the "no turn order" variant: informational banner for
 * free-form games without a defined turn structure.
 */

import type { ReactElement } from 'react';

import type { PlayerInfo, TurnState } from '@/lib/session-live/turn-state';

import type { TurnIndicatorRendererLabels } from '../labels';

interface Props {
  readonly state: Extract<TurnState, { type: 'None' }>;
  readonly players: ReadonlyArray<PlayerInfo>;
  readonly viewerId: string;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorRendererLabels;
}

export function NoneBranch({ compact = false, labels }: Props): ReactElement {
  return (
    <section
      data-slot="turn-branch-none"
      role="region"
      aria-label={labels.noneHeading}
      className={`flex flex-col ${compact ? 'gap-2' : 'gap-3'}`}
    >
      <h4 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
        {labels.noneHeading}
      </h4>
      <div
        role="status"
        className="rounded-lg border border-entity-session/30 bg-entity-session/10 p-3 text-center"
      >
        <p className="text-2xl" aria-hidden="true">
          🎲
        </p>
        <p className="text-sm font-semibold text-entity-session">Nessun ordine di turno</p>
        <p className="text-xs text-muted-foreground">Gioco a struttura libera.</p>
      </div>
    </section>
  );
}
