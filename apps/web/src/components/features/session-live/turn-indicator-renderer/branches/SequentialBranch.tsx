'use client';

/**
 * SequentialBranch — Issue #2378 G5b.
 *
 * Renders sequential turn order: numbered phase stepper showing
 * current phase progression.
 */

import type { ReactElement } from 'react';

import type { PlayerInfo, TurnState } from '@/lib/session-live/turn-state';

import { PhaseStepper } from '../internals/PhaseStepper';

import type { TurnIndicatorRendererLabels } from '../labels';

interface Props {
  readonly state: Extract<TurnState, { type: 'Sequential' }>;
  readonly players: ReadonlyArray<PlayerInfo>;
  readonly viewerId: string;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorRendererLabels;
}

export function SequentialBranch({ state, compact = false, labels }: Props): ReactElement {
  return (
    <section
      data-slot="turn-branch-sequential"
      role="region"
      aria-label={labels.sequentialHeading}
      className={`flex flex-col ${compact ? 'gap-2' : 'gap-3'}`}
    >
      <h4 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
        {labels.sequentialHeading}
      </h4>
      <div aria-live="polite">
        <PhaseStepper phases={state.phases} activeIndex={state.activePhaseIndex} />
      </div>
    </section>
  );
}
