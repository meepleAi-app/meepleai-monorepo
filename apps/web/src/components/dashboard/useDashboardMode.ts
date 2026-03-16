'use client';

import { useContext, useMemo } from 'react';

import { useSelector } from '@xstate/react';

import { DashboardEngineContext } from './DashboardEngineProvider';

import type { DashboardEvent } from './DashboardEngine';

/**
 * Read the current dashboard mode from the DashboardEngine state machine.
 *
 * When used outside a `<DashboardEngineProvider>`, returns safe exploration
 * defaults via the fallback actor (always in exploration state).
 * No conditional hook calls — useSelector is always called.
 */
export function useDashboardMode() {
  const actorRef = useContext(DashboardEngineContext);
  const snapshot = useSelector(actorRef, s => s);

  return useMemo(() => {
    const stateValue = snapshot.value;
    const topState = typeof stateValue === 'string' ? stateValue : 'gameMode';

    return {
      state: topState as 'exploration' | 'transitioning' | 'gameMode',
      isExploration: topState === 'exploration',
      isGameMode: topState === 'gameMode',
      isTransitioning: topState === 'transitioning',
      isExpanded:
        typeof stateValue === 'object' &&
        'gameMode' in stateValue &&
        (stateValue as Record<string, string>).gameMode === 'expanded',
      activeSessionId: snapshot.context.activeSessionId,
      transitionTarget: snapshot.context.transitionTarget,
      send: actorRef.send as (event: DashboardEvent) => void,
    };
  }, [snapshot, actorRef]);
}
