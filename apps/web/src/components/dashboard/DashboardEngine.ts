import { assign, setup } from 'xstate';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type SheetContext = 'scores' | 'rules-ai' | 'timer' | 'photos' | 'players';

export interface BreadcrumbEntry {
  context: SheetContext;
  label: string;
}

export type DashboardEvent =
  | { type: 'SESSION_DETECTED'; sessionId: string }
  | { type: 'TRANSITION_COMPLETE' }
  | { type: 'SESSION_COMPLETED' }
  | { type: 'SESSION_DISMISSED' }
  | { type: 'OPEN_SHEET'; context: SheetContext }
  | { type: 'CLOSE_SHEET' }
  | { type: 'NAVIGATE_CARD_LINK'; target: SheetContext }
  | { type: 'BACK_CARD_LINK' };

export interface DashboardEngineContext {
  activeSessionId: string | null;
  transitionType: 'morph' | 'fade' | 'slide' | 'none';
  transitionTarget: 'exploration' | 'gameMode' | null;
  previousState: 'exploration' | 'gameMode' | null;
  bufferedEvents: DashboardEvent[];
  activeSheet: SheetContext | null;
  breadcrumb: BreadcrumbEntry[];
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

export const SHEET_LABELS: Record<SheetContext, string> = {
  scores: 'Punteggi',
  'rules-ai': 'Regole AI',
  timer: 'Timer',
  photos: 'Foto',
  players: 'Giocatori',
};

export const MAX_BREADCRUMB_DEPTH = 3;

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
      activeSheet: null as SheetContext | null,
      breadcrumb: [] as BreadcrumbEntry[],
    })),
    clearTransitionTarget: assign(() => ({
      transitionTarget: null as 'exploration' | 'gameMode' | null,
    })),
    clearSession: assign(() => ({
      activeSessionId: null,
      transitionTarget: null as 'exploration' | 'gameMode' | null,
      previousState: null as 'exploration' | 'gameMode' | null,
      bufferedEvents: [] as DashboardEvent[],
      activeSheet: null as SheetContext | null,
      breadcrumb: [] as BreadcrumbEntry[],
    })),
    processBuffer: assign(({ context: _context }) => ({
      transitionTarget: 'exploration' as const,
      previousState: 'gameMode' as const,
      bufferedEvents: [] as DashboardEvent[],
    })),
    setSheet: assign(({ event }) => {
      if (event.type !== 'OPEN_SHEET') return {};
      const ctx = event.context;
      return {
        activeSheet: ctx,
        breadcrumb: [{ context: ctx, label: SHEET_LABELS[ctx] }] as BreadcrumbEntry[],
      };
    }),
    clearSheet: assign(() => ({
      activeSheet: null as SheetContext | null,
      breadcrumb: [] as BreadcrumbEntry[],
    })),
    pushBreadcrumb: assign(({ context, event }) => {
      if (event.type !== 'NAVIGATE_CARD_LINK') return {};
      const target = event.target;
      const newEntry: BreadcrumbEntry = { context: target, label: SHEET_LABELS[target] };
      return {
        activeSheet: target,
        breadcrumb: [...context.breadcrumb, newEntry],
      };
    }),
    popBreadcrumb: assign(({ context }) => {
      const newBreadcrumb = context.breadcrumb.slice(0, -1);
      const previous = newBreadcrumb[newBreadcrumb.length - 1];
      return {
        activeSheet: previous?.context ?? null,
        breadcrumb: newBreadcrumb,
      };
    }),
  },
  guards: {
    targetIsGameMode: ({ context }) => context.transitionTarget === 'gameMode',
    targetIsExploration: ({ context }) => context.transitionTarget === 'exploration',
    hasBufferedTermination: ({ context }) => context.bufferedEvents.some(isTerminationEvent),
    breadcrumbNotFull: ({ context }) => context.breadcrumb.length < MAX_BREADCRUMB_DEPTH,
    hasBreadcrumbHistory: ({ context }) => context.breadcrumb.length > 1,
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
    activeSheet: null,
    breadcrumb: [],
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
      initial: 'tavolo',
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
        tavolo: {
          entry: 'clearSheet',
          on: {
            OPEN_SHEET: {
              target: 'sheetOpen',
              actions: 'setSheet',
            },
          },
        },
        sheetOpen: {
          on: {
            CLOSE_SHEET: {
              target: 'tavolo',
            },
            OPEN_SHEET: {
              actions: 'setSheet',
            },
            NAVIGATE_CARD_LINK: {
              guard: 'breadcrumbNotFull',
              actions: 'pushBreadcrumb',
            },
            BACK_CARD_LINK: {
              guard: 'hasBreadcrumbHistory',
              actions: 'popBreadcrumb',
            },
          },
        },
      },
    },
  },
});
