'use client';

/**
 * CustomBranch — Issue #2378 G5b.
 *
 * Renders toolkit-driven custom turn order: same PhaseStepper as
 * Sequential but with custom-specific heading.
 */

import type { ReactElement } from 'react';

import type { PlayerInfo, TurnState } from '@/lib/session-live/turn-state';

import { PhaseStepper } from '../internals/PhaseStepper';

import type { TurnIndicatorRendererLabels } from '../labels';

interface Props {
  readonly state: Extract<TurnState, { type: 'Custom' }>;
  readonly players: ReadonlyArray<PlayerInfo>;
  readonly viewerId: string;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorRendererLabels;
}

export function CustomBranch({ state, compact = false, labels }: Props): ReactElement {
  return (
    <section
      data-slot="turn-branch-custom"
      role="region"
      aria-label={labels.customHeading}
      className={`flex flex-col ${compact ? 'gap-2' : 'gap-3'}`}
    >
      <h4 className="text-sm font-semibold uppercase tracking-wider text-muted-foreground">
        {labels.customHeading}
      </h4>
      <div aria-live="polite">
        <PhaseStepper phases={state.phases} activeIndex={state.activePhaseIndex} />
      </div>
    </section>
  );
}
