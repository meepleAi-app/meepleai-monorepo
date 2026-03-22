'use client';

import { useCallback, useContext } from 'react';

import { useSelector } from '@xstate/react';

import { DashboardEngineContext } from './DashboardEngineProvider';

import type { DashboardEvent, SheetContext, BreadcrumbEntry } from './DashboardEngine';

/**
 * Read the current dashboard mode from the DashboardEngine state machine
 * and expose sheet navigation actions.
 *
 * When used outside a `<DashboardEngineProvider>`, returns safe exploration
 * defaults via the fallback actor (always in exploration state).
 * No conditional hook calls — useSelector is always called.
 */
export function useDashboardMode() {
  const actorRef = useContext(DashboardEngineContext);
  const snapshot = useSelector(actorRef, s => s);

  const send = actorRef.send as (event: DashboardEvent) => void;

  const openSheet = useCallback(
    (context: SheetContext) => send({ type: 'OPEN_SHEET', context }),
    [send]
  );

  const closeSheet = useCallback(() => send({ type: 'CLOSE_SHEET' }), [send]);

  const navigateCardLink = useCallback(
    (target: SheetContext) => send({ type: 'NAVIGATE_CARD_LINK', target }),
    [send]
  );

  const backCardLink = useCallback(() => send({ type: 'BACK_CARD_LINK' }), [send]);

  const stateValue = snapshot.value;
  const topState = typeof stateValue === 'string' ? stateValue : 'gameMode';

  return {
    state: topState as 'exploration' | 'transitioning' | 'gameMode',
    isExploration: topState === 'exploration',
    isGameMode: topState === 'gameMode',
    isTransitioning: topState === 'transitioning',
    activeSessionId: snapshot.context.activeSessionId,
    transitionTarget: snapshot.context.transitionTarget,
    activeSheet: snapshot.context.activeSheet as SheetContext | null,
    breadcrumb: snapshot.context.breadcrumb as BreadcrumbEntry[],
    send,
    openSheet,
    closeSheet,
    navigateCardLink,
    backCardLink,
  };
}
