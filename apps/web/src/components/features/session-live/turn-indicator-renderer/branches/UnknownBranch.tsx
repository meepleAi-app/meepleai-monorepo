'use client';

/**
 * UnknownBranch — Issue #2378 G5b.
 *
 * Fallback rendered when TurnState.type does not match any known
 * TurnOrderType variant. Shows a dev-visible warning with the raw type.
 */

import type { ReactElement } from 'react';

import type { TurnState } from '@/lib/session-live/turn-state';

import type { TurnIndicatorRendererLabels } from '../labels';

interface Props {
  readonly state: TurnState;
  readonly labels: TurnIndicatorRendererLabels;
}

export function UnknownBranch({ state, labels }: Props): ReactElement {
  return (
    <section
      data-slot="turn-branch-unknown"
      role="status"
      aria-live="polite"
      className="flex flex-col gap-2 rounded-lg border border-amber-700/30 bg-amber-900/10 p-3"
    >
      <p className="text-sm font-bold text-amber-200">{labels.unknownTitle}</p>
      <p className="text-xs text-amber-100/70">{labels.unknownBody}</p>
      <code className="text-xs text-amber-100/50">
        turnOrderType: {(state as { type: string }).type}
      </code>
    </section>
  );
}
