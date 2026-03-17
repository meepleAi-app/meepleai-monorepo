'use client';

import { createContext, useEffect, useRef } from 'react';

import { useActorRef } from '@xstate/react';
import { createActor, type ActorRefFrom } from 'xstate';

import { useSessionStore } from '@/lib/stores/sessionStore';

import { dashboardMachine } from './DashboardEngine';

export type DashboardActorRef = ActorRefFrom<typeof dashboardMachine>;

/**
 * Fallback actor: always in exploration state, send is a noop.
 * Used as default context value so useDashboardMode never needs
 * a conditional hook call.
 */
const fallbackActor = createActor(dashboardMachine);
fallbackActor.start();

export const DashboardEngineContext = createContext<DashboardActorRef>(fallbackActor);

export function DashboardEngineProvider({ children }: { children: React.ReactNode }) {
  const actorRef = useActorRef(dashboardMachine);

  // Session detection: track null->non-null and non-null->null transitions
  const activeSession = useSessionStore(s => s.activeSession);
  const prevSessionRef = useRef(activeSession);

  useEffect(() => {
    const prev = prevSessionRef.current;
    prevSessionRef.current = activeSession;

    if (!prev && activeSession) {
      actorRef.send({ type: 'SESSION_DETECTED', sessionId: activeSession.id });
    } else if (prev && !activeSession) {
      actorRef.send({ type: 'SESSION_COMPLETED' });
    }
  }, [activeSession, actorRef]);

  // Handle initial mount: if session already active
  useEffect(() => {
    const session = useSessionStore.getState().activeSession;
    if (session) {
      actorRef.send({ type: 'SESSION_DETECTED', sessionId: session.id });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <DashboardEngineContext.Provider value={actorRef}>{children}</DashboardEngineContext.Provider>
  );
}
