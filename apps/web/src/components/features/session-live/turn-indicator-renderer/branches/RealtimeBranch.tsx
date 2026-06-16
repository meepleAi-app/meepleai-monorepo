'use client';

/**
 * RealtimeBranch — Issue #2378 G5b.
 *
 * Renders real-time turn order: warning banner indicating no discrete
 * turns exist (simultaneous real-time play).
 */

import type { ReactElement } from 'react';

import type { PlayerInfo, TurnState } from '@/lib/session-live/turn-state';

import type { TurnIndicatorRendererLabels } from '../labels';

interface Props {
  readonly state: Extract<TurnState, { type: 'Realtime' }>;
  readonly players: ReadonlyArray<PlayerInfo>;
  readonly viewerId: string;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorRendererLabels;
}

export function RealtimeBranch({ compact = false, labels }: Props): ReactElement {
  return (
    <section
      data-slot="turn-branch-realtime"
      role="region"
      aria-label={labels.realtimeHeading}
      className={`flex flex-col ${compact ? 'gap-2' : 'gap-3'}`}
    >
      <h4 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
        {labels.realtimeHeading}
      </h4>
      <div
        role="status"
        className="rounded-lg border border-amber-700/30 bg-amber-900/10 p-3 text-center"
      >
        <p className="text-2xl" aria-hidden="true">
          ⏱
        </p>
        <p className="text-sm font-semibold text-amber-200">Nessun turno</p>
        <p className="text-xs text-amber-100/70">Partita in tempo reale e simultanea.</p>
      </div>
    </section>
  );
}
