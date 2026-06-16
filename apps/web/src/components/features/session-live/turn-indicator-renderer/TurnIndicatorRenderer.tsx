'use client';

/**
 * TurnIndicatorRenderer — Issue #2378 G5b polymorphic dispatcher.
 *
 * Switches on `state.type` (`TurnOrderType`) and renders one of 7 branch
 * components, or an `UnknownBranch` fallback for unregistered values.
 *
 * §5 contract (epic #2354 G5):
 *   - data-slot="turn-indicator" on the root <section>
 *   - children are sub-section data-slot="turn-branch-<kebab>"
 *   - No SSE state inside the dispatcher (parent owns the state)
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2378-g5b-turn-indicator-renderer-design.md §3
 */

import type { ReactElement } from 'react';

import type { TurnState, PlayerInfo, TurnOrderType } from '@/lib/session-live/turn-state';

import { CustomBranch } from './branches/CustomBranch';
import { FirstPlayerTokenBranch } from './branches/FirstPlayerTokenBranch';
import { NoneBranch } from './branches/NoneBranch';
import { RealtimeBranch } from './branches/RealtimeBranch';
import { RoundRobinBranch } from './branches/RoundRobinBranch';
import { SequentialBranch } from './branches/SequentialBranch';
import { SimultaneousBranch } from './branches/SimultaneousBranch';
import { UnknownBranch } from './branches/UnknownBranch';

import type { TurnIndicatorRendererLabels } from './labels';

export type { TurnIndicatorRendererLabels };

export interface TurnIndicatorRendererProps {
  readonly state: TurnState;
  readonly players: ReadonlyArray<PlayerInfo>;
  readonly viewerId: string;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorRendererLabels;
}

const KNOWN_TYPES: ReadonlySet<TurnOrderType> = new Set([
  'RoundRobin',
  'Sequential',
  'Simultaneous',
  'Realtime',
  'None',
  'Custom',
  'FirstPlayerToken',
]);

export function TurnIndicatorRenderer({
  state,
  players,
  viewerId,
  compact,
  labels,
}: TurnIndicatorRendererProps): ReactElement {
  const isKnown = KNOWN_TYPES.has(state.type as TurnOrderType);

  if (!isKnown) {
    // Defensive: surface unknown types in logs so operators see new BE enums.
    console.warn(
      '[TurnIndicatorRenderer] Unknown turnOrderType:',
      (state as { type: string }).type
    );
    return (
      <section data-slot="turn-indicator" role="region" aria-label={labels.unknownTitle}>
        <UnknownBranch state={state} labels={labels} />
      </section>
    );
  }

  // Compute heading for aria-label.
  let heading: string;
  switch (state.type) {
    case 'RoundRobin':
      heading = labels.roundRobinHeading;
      break;
    case 'Sequential':
      heading = labels.sequentialHeading;
      break;
    case 'Simultaneous':
      heading = labels.simultaneousHeading;
      break;
    case 'Realtime':
      heading = labels.realtimeHeading;
      break;
    case 'None':
      heading = labels.noneHeading;
      break;
    case 'Custom':
      heading = labels.customHeading;
      break;
    case 'FirstPlayerToken':
      heading = labels.firstPlayerTokenHeading;
      break;
  }

  return (
    <section data-slot="turn-indicator" role="region" aria-label={heading}>
      {state.type === 'RoundRobin' && (
        <RoundRobinBranch
          state={state}
          players={players}
          viewerId={viewerId}
          compact={compact}
          labels={labels}
        />
      )}
      {state.type === 'Sequential' && (
        <SequentialBranch
          state={state}
          players={players}
          viewerId={viewerId}
          compact={compact}
          labels={labels}
        />
      )}
      {state.type === 'Simultaneous' && (
        <SimultaneousBranch
          state={state}
          players={players}
          viewerId={viewerId}
          compact={compact}
          labels={labels}
        />
      )}
      {state.type === 'Realtime' && (
        <RealtimeBranch
          state={state}
          players={players}
          viewerId={viewerId}
          compact={compact}
          labels={labels}
        />
      )}
      {state.type === 'None' && (
        <NoneBranch
          state={state}
          players={players}
          viewerId={viewerId}
          compact={compact}
          labels={labels}
        />
      )}
      {state.type === 'Custom' && (
        <CustomBranch
          state={state}
          players={players}
          viewerId={viewerId}
          compact={compact}
          labels={labels}
        />
      )}
      {state.type === 'FirstPlayerToken' && (
        <FirstPlayerTokenBranch
          state={state}
          players={players}
          viewerId={viewerId}
          compact={compact}
          labels={labels}
        />
      )}
    </section>
  );
}
