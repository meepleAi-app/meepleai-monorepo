import { createActor } from 'xstate';

import {
  dashboardMachine,
  type DashboardEngineContext,
  type DashboardEvent,
} from '../DashboardEngine';

function createTestActor() {
  return createActor(dashboardMachine);
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
    it('transitions from transitioning to gameMode on TRANSITION_COMPLETE', () => {
      const actor = createTestActor();
      actor.start();

      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      actor.send({ type: 'TRANSITION_COMPLETE' });

      const snapshot = actor.getSnapshot();
      expect(snapshot.value).toEqual({ gameMode: 'default' });
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
      expect(actor.getSnapshot().value).toEqual({ gameMode: 'default' });

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

  describe('gameMode sub-states (EXPAND / COLLAPSE)', () => {
    it('starts in default sub-state', () => {
      const actor = createTestActor();
      actor.start();

      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      actor.send({ type: 'TRANSITION_COMPLETE' });

      expect(actor.getSnapshot().value).toEqual({ gameMode: 'default' });

      actor.stop();
    });

    it('transitions to expanded on EXPAND', () => {
      const actor = createTestActor();
      actor.start();

      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      actor.send({ type: 'TRANSITION_COMPLETE' });
      actor.send({ type: 'EXPAND' });

      expect(actor.getSnapshot().value).toEqual({ gameMode: 'expanded' });

      actor.stop();
    });

    it('transitions back to default on COLLAPSE', () => {
      const actor = createTestActor();
      actor.start();

      actor.send({ type: 'SESSION_DETECTED', sessionId: 'session-123' });
      actor.send({ type: 'TRANSITION_COMPLETE' });
      actor.send({ type: 'EXPAND' });
      actor.send({ type: 'COLLAPSE' });

      expect(actor.getSnapshot().value).toEqual({ gameMode: 'default' });

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
});
