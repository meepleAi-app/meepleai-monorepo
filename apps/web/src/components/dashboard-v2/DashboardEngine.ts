import { assign, setup } from 'xstate';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type DashboardEvent =
  | { type: 'SESSION_DETECTED'; sessionId: string }
  | { type: 'TRANSITION_COMPLETE' }
  | { type: 'SESSION_COMPLETED' }
  | { type: 'SESSION_DISMISSED' }
  | { type: 'EXPAND' }
  | { type: 'COLLAPSE' };

export interface DashboardEngineContext {
  activeSessionId: string | null;
  transitionType: 'morph' | 'fade' | 'slide' | 'none';
  transitionTarget: 'exploration' | 'gameMode' | null;
  previousState: 'exploration' | 'gameMode' | null;
  bufferedEvents: DashboardEvent[];
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

type TerminationEvent = Extract<
  DashboardEvent,
  { type: 'SESSION_COMPLETED' | 'SESSION_DISMISSED' }
>;

function isTerminationEvent(evt: DashboardEvent): evt is TerminationEvent {
  return evt.type === 'SESSION_COMPLETED' || evt.type === 'SESSION_DISMISSED';
}

// ---------------------------------------------------------------------------
// Machine
// ---------------------------------------------------------------------------

export const dashboardMachine = setup({
  types: {
    context: {} as DashboardEngineContext,
    events: {} as DashboardEvent,
  },
  actions: {
    setSessionDetected: assign(({ context: _context, event }) => {
      if (event.type !== 'SESSION_DETECTED') return {};
      return {
        activeSessionId: event.sessionId,
        transitionTarget: 'gameMode' as const,
        previousState: 'exploration' as const,
        transitionType: 'morph' as const,
        bufferedEvents: [],
      };
    }),
    bufferTerminationEvent: assign(({ context, event }) => {
      if (!isTerminationEvent(event)) return {};
      return {
        bufferedEvents: [...context.bufferedEvents, event],
      };
    }),
    beginExitTransition: assign(({ context: _context }) => ({
      transitionTarget: 'exploration' as const,
      previousState: 'gameMode' as const,
      transitionType: 'fade' as const,
    })),
    clearTransitionTarget: assign(() => ({
      transitionTarget: null as 'exploration' | 'gameMode' | null,
    })),
    clearSession: assign(() => ({
      activeSessionId: null,
      transitionTarget: null as 'exploration' | 'gameMode' | null,
      previousState: null as 'exploration' | 'gameMode' | null,
      bufferedEvents: [] as DashboardEvent[],
    })),
    processBuffer: assign(({ context: _context }) => ({
      transitionTarget: 'exploration' as const,
      previousState: 'gameMode' as const,
      bufferedEvents: [] as DashboardEvent[],
    })),
  },
  guards: {
    targetIsGameMode: ({ context }) => context.transitionTarget === 'gameMode',
    targetIsExploration: ({ context }) => context.transitionTarget === 'exploration',
    hasBufferedTermination: ({ context }) => context.bufferedEvents.some(isTerminationEvent),
  },
}).createMachine({
  id: 'dashboard',
  initial: 'exploration',
  context: {
    activeSessionId: null,
    transitionType: 'morph',
    transitionTarget: null,
    previousState: null,
    bufferedEvents: [],
  },
  states: {
    exploration: {
      entry: 'clearSession',
      on: {
        SESSION_DETECTED: {
          target: 'transitioning',
          actions: 'setSessionDetected',
        },
      },
    },

    transitioning: {
      on: {
        SESSION_COMPLETED: {
          actions: 'bufferTerminationEvent',
        },
        SESSION_DISMISSED: {
          actions: 'bufferTerminationEvent',
        },
        TRANSITION_COMPLETE: [
          // If there are buffered termination events, process them and re-enter
          {
            target: 'transitioning',
            guard: 'hasBufferedTermination',
            actions: 'processBuffer',
            reenter: true,
          },
          // Normal completion: go to target state
          {
            target: 'gameMode',
            guard: 'targetIsGameMode',
            actions: 'clearTransitionTarget',
          },
          {
            target: 'exploration',
            guard: 'targetIsExploration',
          },
        ],
      },
    },

    gameMode: {
      initial: 'default',
      on: {
        SESSION_COMPLETED: {
          target: 'transitioning',
          actions: 'beginExitTransition',
        },
        SESSION_DISMISSED: {
          target: 'transitioning',
          actions: 'beginExitTransition',
        },
      },
      states: {
        default: {
          on: {
            EXPAND: { target: 'expanded' },
          },
        },
        expanded: {
          on: {
            COLLAPSE: { target: 'default' },
          },
        },
      },
    },
  },
});
