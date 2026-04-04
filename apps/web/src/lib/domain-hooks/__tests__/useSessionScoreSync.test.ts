import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';
import { useSessionScoreSync } from '../useSessionScoreSync';
import { useSessionStore } from '@/stores/session/store';

describe('useSessionScoreSync', () => {
  beforeEach(() => {
    localStorage.clear();
    useSessionStore.getState().reset();
    useSessionStore.getState().startSession({
      sessionId: 'sess-1',
      gameId: 'g',
      gameTitle: 'Catan',
      participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
    });
  });

  it('aggiorna score nello store quando riceve evento SSE score_update', () => {
    const { result } = renderHook(() => useSessionScoreSync());

    act(() => {
      result.current.handleSseEvent({
        id: 'evt-1',
        type: 'score_update',
        playerId: 'p1',
        data: { score: 8 },
        timestamp: new Date().toISOString(),
      });
    });

    const score = useSessionStore.getState().scores.find(s => s.playerId === 'p1');
    expect(score?.score).toBe(8);

    // handleSseEvent no longer calls addEvent — useSessionSSE owns that responsibility.
    // When called directly (not through useSessionSSE), the activity feed stays empty.
    const events = useSessionStore.getState().events;
    expect(events.length).toBe(0);
  });
});
