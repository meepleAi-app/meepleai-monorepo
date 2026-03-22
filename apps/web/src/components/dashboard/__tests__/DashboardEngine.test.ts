import { createActor } from 'xstate';

import {
  dashboardMachine,
  MAX_BREADCRUMB_DEPTH,
  type DashboardEngineContext,
  type DashboardEvent,
  type SheetContext,
} from '../DashboardEngine';

function createTestActor() {
  return createActor(dashboardMachine);
}

/** Helper: bring actor to gameMode.tavolo */
function toTavolo() {
  const actor = createTestActor();
  actor.start();
  actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
  actor.send({ type: 'TRANSITION_COMPLETE' });
  return actor;
}

describe('DashboardEngine', () => {
  describe('initial state', () => {
    it('starts in exploration state', () => {
      const actor = createTestActor();
      actor.start();

      expect(actor.getSnapshot().value).toBe('exploration');
      expect(actor.getSnapshot().context.activeSessionId).toBeNull();
      expect(actor.getSnapshot().context.transitionTarget).toBeNull();
      expect(actor.getSnapshot().context.previousState).toBeNull();
      expect(actor.getSnapshot().context.bufferedEvents).toEqual([]);

      actor.stop();
    });
  });

  describe('SESSION_DETECTED → transitioning', () => {
    it('transitions from exploration to transitioning with correct context', () => {
      const actor = createTestActor();
      actor.start();

      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toBe('transitioning');
      expect(snapshot.context.activeSessionId).toBe('session-123');
      expect(snapshot.context.transitionTarget).toBe('gameMode');
      expect(snapshot.context.previousState).toBe('exploration');
      expect(snapshot.context.transitionType).toBe('morph');

      actor.stop();
    });
  });

  describe('TRANSITION_COMPLETE → gameMode', () => {
    it('transitions from transitioning to gameMode.tavolo on TRANSITION_COMPLETE', () => {
      const actor = createTestActor();
      actor.start();

      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      actor.send({ type: 'TRANSITION_COMPLETE' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toEqual({ gameMode: 'tavolo' });
      expect(snapshot.context.transitionTarget).toBeNull();

      actor.stop();
    });
  });

  describe('gameMode → SESSION_COMPLETED → transitioning', () => {
    it('transitions from gameMode to transitioning on SESSION_COMPLETED', () => {
      const actor = createTestActor();
      actor.start();

      // Go to gameMode
      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      actor.send({ type: 'TRANSITION_COMPLETE' });
      expect(actor.getSnapshot().value).toEqual({ gameMode: 'tavolo' });

      // End session
      actor.send({ type: 'SESSION_COMPLETED' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toBe('transitioning');
      expect(snapshot.context.transitionTarget).toBe('exploration');
      expect(snapshot.context.previousState).toBe('gameMode');

      actor.stop();
    });

    it('transitions from gameMode to transitioning on SESSION_DISMISSED', () => {
      const actor = createTestActor();
      actor.start();

      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      actor.send({ type: 'TRANSITION_COMPLETE' });
      actor.send({ type: 'SESSION_DISMISSED' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toBe('transitioning');
      expect(snapshot.context.transitionTarget).toBe('exploration');
      expect(snapshot.context.previousState).toBe('gameMode');

      actor.stop();
    });
  });

  describe('buffered events during transitioning', () => {
    it('buffers SESSION_COMPLETED when already transitioning into gameMode', () => {
      const actor = createTestActor();
      actor.start();

      // Start transition to gameMode
      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      expect(actor.getSnapshot().value).toBe('transitioning');

      // While transitioning, session ends — should be buffered
      actor.send({ type: 'SESSION_COMPLETED' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toBe('transitioning');
      expect(snapshot.context.bufferedEvents).toHaveLength(1);
      expect(snapshot.context.bufferedEvents[0]).toEqual({ type: 'SESSION_COMPLETED' });
    });

    it('processes buffered events on TRANSITION_COMPLETE with reenter', () => {
      const actor = createTestActor();
      actor.start();

      // Start transition to gameMode
      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });

      // Buffer a session end while transitioning
      actor.send({ type: 'SESSION_COMPLETED' });
      expect(actor.getSnapshot().context.bufferedEvents).toHaveLength(1);

      // Complete transition — should process buffered event and re-enter transitioning
      actor.send({ type: 'TRANSITION_COMPLETE' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toBe('transitioning');
      expect(snapshot.context.transitionTarget).toBe('exploration');
      expect(snapshot.context.bufferedEvents).toEqual([]);
    });

    it('buffers SESSION_DISMISSED when already transitioning', () => {
      const actor = createTestActor();
      actor.start();

      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      actor.send({ type: 'SESSION_DISMISSED' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toBe('transitioning');
      expect(snapshot.context.bufferedEvents).toHaveLength(1);
      expect(snapshot.context.bufferedEvents[0]).toEqual({ type: 'SESSION_DISMISSED' });
    });
  });

  describe('gameMode sub-states (tavolo / sheetOpen)', () => {
    it('starts in tavolo sub-state', () => {
      const actor = createTestActor();
      actor.start();

      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      actor.send({ type: 'TRANSITION_COMPLETE' });

      expect(actor.getSnapshot().value).toEqual({ gameMode: 'tavolo' });

      actor.stop();
    });
  });

  describe('clearSession on exploration entry', () => {
    it('clears activeSessionId when returning to exploration', () => {
      const actor = createTestActor();
      actor.start();

      // Full cycle: exploration → gameMode → exploration
      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      actor.send({ type: 'TRANSITION_COMPLETE' });
      expect(actor.getSnapshot().context.activeSessionId).toBe('session-123');

      actor.send({ type: 'SESSION_COMPLETED' });
      actor.send({ type: 'TRANSITION_COMPLETE' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toBe('exploration');
      expect(snapshot.context.activeSessionId).toBeNull();
      expect(snapshot.context.transitionTarget).toBeNull();

      actor.stop();
    });
  });

  describe('TRANSITION_COMPLETE to exploration (no buffered events)', () => {
    it('goes to exploration when transitionTarget is exploration and no buffered events', () => {
      const actor = createTestActor();
      actor.start();

      // Go to gameMode
      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      actor.send({ type: 'TRANSITION_COMPLETE' });

      // End session → transitioning with target exploration
      actor.send({ type: 'SESSION_COMPLETED' });
      expect(actor.getSnapshot().context.transitionTarget).toBe('exploration');

      // Complete transition
      actor.send({ type: 'TRANSITION_COMPLETE' });

      expect(actor.getSnapshot().value).toBe('exploration');

      actor.stop();
    });
  });

  describe('DashboardEngine — sheet navigation', () => {
    it('transitions from tavolo to sheetOpen on OPEN_SHEET', () => {
      const actor = toTavolo();

      actor.send({ type: 'OPEN_SHEET', context: 'scores' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toEqual({ gameMode: 'sheetOpen' });
      expect(snapshot.context.activeSheet).toBe('scores');
      expect(snapshot.context.breadcrumb).toHaveLength(1);
      expect(snapshot.context.breadcrumb[0]).toEqual({ context: 'scores', label: 'Punteggi' });

      actor.stop();
    });

    it('transitions from sheetOpen to tavolo on CLOSE_SHEET', () => {
      const actor = toTavolo();
      actor.send({ type: 'OPEN_SHEET', context: 'scores' });
      actor.send({ type: 'CLOSE_SHEET' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toEqual({ gameMode: 'tavolo' });
      expect(snapshot.context.activeSheet).toBeNull();
      expect(snapshot.context.breadcrumb).toEqual([]);

      actor.stop();
    });

    it('pushes breadcrumb on NAVIGATE_CARD_LINK', () => {
      const actor = toTavolo();
      actor.send({ type: 'OPEN_SHEET', context: 'scores' });
      actor.send({ type: 'NAVIGATE_CARD_LINK', target: 'rules-ai' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toEqual({ gameMode: 'sheetOpen' });
      expect(snapshot.context.activeSheet).toBe('rules-ai');
      expect(snapshot.context.breadcrumb).toHaveLength(2);
      expect(snapshot.context.breadcrumb[1]).toEqual({ context: 'rules-ai', label: 'Regole AI' });

      actor.stop();
    });

    it('pops breadcrumb on BACK_CARD_LINK', () => {
      const actor = toTavolo();
      actor.send({ type: 'OPEN_SHEET', context: 'scores' });
      actor.send({ type: 'NAVIGATE_CARD_LINK', target: 'rules-ai' });
      actor.send({ type: 'BACK_CARD_LINK' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.context.activeSheet).toBe('scores');
      expect(snapshot.context.breadcrumb).toHaveLength(1);
      expect(snapshot.context.breadcrumb[0]).toEqual({ context: 'scores', label: 'Punteggi' });

      actor.stop();
    });

    it(`caps breadcrumb at max depth ${MAX_BREADCRUMB_DEPTH}`, () => {
      const actor = toTavolo();
      actor.send({ type: 'OPEN_SHEET', context: 'scores' }); // depth 1
      actor.send({ type: 'NAVIGATE_CARD_LINK', target: 'rules-ai' }); // depth 2
      actor.send({ type: 'NAVIGATE_CARD_LINK', target: 'timer' }); // depth 3
      actor.send({ type: 'NAVIGATE_CARD_LINK', target: 'photos' }); // should be blocked

      const snapshot = actor.getSnapshot();
      expect(snapshot.context.breadcrumb).toHaveLength(MAX_BREADCRUMB_DEPTH);
      expect(snapshot.context.activeSheet).toBe('timer'); // unchanged

      actor.stop();
    });

    it('switches sheet context when OPEN_SHEET sent while sheetOpen', () => {
      const actor = toTavolo();
      actor.send({ type: 'OPEN_SHEET', context: 'scores' });
      actor.send({ type: 'OPEN_SHEET', context: 'timer' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toEqual({ gameMode: 'sheetOpen' });
      expect(snapshot.context.activeSheet).toBe('timer');
      expect(snapshot.context.breadcrumb).toHaveLength(1);
      expect(snapshot.context.breadcrumb[0]).toEqual({ context: 'timer', label: 'Timer' });

      actor.stop();
    });

    it('clears sheet state on SESSION_COMPLETED', () => {
      const actor = toTavolo();
      actor.send({ type: 'OPEN_SHEET', context: 'scores' });
      actor.send({ type: 'SESSION_COMPLETED' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toBe('transitioning');
      expect(snapshot.context.activeSheet).toBeNull();
      expect(snapshot.context.breadcrumb).toEqual([]);

      actor.stop();
    });
  });
});
