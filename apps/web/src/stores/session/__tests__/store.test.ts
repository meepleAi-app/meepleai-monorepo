import { describe, expect, it, beforeEach } from 'vitest';
import { act } from '@testing-library/react';
import { useSessionStore } from '../store';

const makePayload = (
  overrides?: Partial<{ sessionId: string; gameId: string; gameTitle: string }>
) => ({
  sessionId: overrides?.sessionId ?? 'session-1',
  gameId: overrides?.gameId ?? 'game-1',
  gameTitle: overrides?.gameTitle ?? 'Test Game',
  participants: [],
});

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
    expect(state.participants).toEqual([]);
    expect(state.gameTitle).toBeNull();
  });

  it('starts a session', () => {
    useSessionStore
      .getState()
      .startSession(makePayload({ sessionId: 'session-1', gameId: 'game-1', gameTitle: 'Catan' }));
    const state = useSessionStore.getState();
    expect(state.status).toBe('live');
    expect(state.sessionId).toBe('session-1');
    expect(state.gameId).toBe('game-1');
    expect(state.gameTitle).toBe('Catan');
  });

  it('toggles pause', () => {
    useSessionStore.getState().startSession(makePayload());
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
    useSessionStore.getState().startSession(makePayload());
    useSessionStore.getState().endSession();
    expect(useSessionStore.getState().status).toBe('ended');
  });
});

describe('useSessionStore — partecipanti e gioco', () => {
  beforeEach(() => useSessionStore.getState().reset());

  it('startSession persiste gameTitle e participants', () => {
    act(() => {
      useSessionStore.getState().startSession({
        sessionId: 'sess-1',
        gameId: 'game-1',
        gameTitle: 'Catan',
        participants: [
          { id: 'p1', displayName: 'Marco', isGuest: false },
          { id: 'p2', displayName: 'Luca (ospite)', isGuest: true },
        ],
      });
    });

    const s = useSessionStore.getState();
    expect(s.gameTitle).toBe('Catan');
    expect(s.participants).toHaveLength(2);
    expect(s.participants[1].isGuest).toBe(true);
    expect(s.status).toBe('live');
  });

  it('updateScore aggiorna score di un partecipante', () => {
    act(() => {
      useSessionStore.getState().startSession({
        sessionId: 's',
        gameId: 'g',
        gameTitle: 'Dixit',
        participants: [{ id: 'p1', displayName: 'Sara', isGuest: false }],
      });
      useSessionStore.getState().updateScore('p1', 7);
    });

    const score = useSessionStore.getState().scores.find(s => s.playerId === 'p1');
    expect(score?.score).toBe(7);
  });

  it('reset pulisce tutti i campi', () => {
    act(() => {
      useSessionStore.getState().startSession({
        sessionId: 's',
        gameId: 'g',
        gameTitle: 'G',
        participants: [],
      });
      useSessionStore.getState().reset();
    });

    const s = useSessionStore.getState();
    expect(s.sessionId).toBeNull();
    expect(s.gameTitle).toBeNull();
    expect(s.participants).toHaveLength(0);
  });
});
