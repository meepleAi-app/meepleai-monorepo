import { describe, expect, it, beforeEach } from 'vitest';
import { useSessionStore } from '../store';

describe('useSessionStore', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('starts in idle state', () => {
    const state = useSessionStore.getState();
    expect(state.status).toBe('idle');
    expect(state.isPaused).toBe(false);
    expect(state.events).toEqual([]);
    expect(state.scores).toEqual([]);
  });

  it('starts a session', () => {
    useSessionStore.getState().startSession('session-1', 'game-1');
    const state = useSessionStore.getState();
    expect(state.status).toBe('live');
    expect(state.sessionId).toBe('session-1');
    expect(state.gameId).toBe('game-1');
  });

  it('toggles pause', () => {
    useSessionStore.getState().startSession('s1', 'g1');
    useSessionStore.getState().togglePause();
    expect(useSessionStore.getState().isPaused).toBe(true);
    useSessionStore.getState().togglePause();
    expect(useSessionStore.getState().isPaused).toBe(false);
  });

  it('adds events to the feed', () => {
    useSessionStore.getState().addEvent({
      id: 'e1',
      type: 'dice_roll',
      playerId: 'p1',
      data: { values: [3, 4], total: 7 },
      timestamp: new Date().toISOString(),
    } as any);
    expect(useSessionStore.getState().events).toHaveLength(1);
  });

  it('updates scores', () => {
    useSessionStore.getState().updateScore('p1', 10);
    expect(useSessionStore.getState().scores).toEqual([{ playerId: 'p1', score: 10 }]);
    useSessionStore.getState().updateScore('p1', 15);
    expect(useSessionStore.getState().scores).toEqual([{ playerId: 'p1', score: 15 }]);
  });

  it('increments current turn', () => {
    useSessionStore.getState().nextTurn();
    expect(useSessionStore.getState().currentTurn).toBe(2);
  });

  it('ends session', () => {
    useSessionStore.getState().startSession('s1', 'g1');
    useSessionStore.getState().endSession();
    expect(useSessionStore.getState().status).toBe('ended');
  });
});
