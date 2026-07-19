import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useGameNightOrchestrator } from '../useGameNightOrchestrator';
import { createSession, goLive } from '@/lib/api/clients/gameSessionsClient';
import { useSessionStore } from '@/stores/session/store';

// Mock API call
vi.mock('@/lib/api/clients/gameSessionsClient', () => ({
  createSession: vi.fn().mockResolvedValue({ sessionId: 'new-sess-1', code: 'ABC-123' }),
  finalizeSession: vi.fn().mockResolvedValue(undefined),
  // Epic #3188 Slice 3 (D5): startGame now creates a DRAFT then promotes it via go-live.
  goLive: vi.fn().mockResolvedValue({
    sessionId: 'new-sess-1',
    gameNightId: 'night-1',
    gameNightSessionId: 'gns-1',
    playOrder: 1,
    status: 'InProgress',
  }),
}));

describe('useGameNightOrchestrator', () => {
  beforeEach(() => {
    localStorage.clear();
    useSessionStore.getState().reset();
    vi.mocked(createSession).mockClear();
    vi.mocked(goLive).mockClear();
  });

  it('startGame crea una sessione DRAFT, la porta live e aggiorna lo store', async () => {
    const { result } = renderHook(() => useGameNightOrchestrator('night-1'));

    await act(async () => {
      await result.current.startGame({
        gameId: 'game-1',
        gameTitle: 'Catan',
        participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
      });
    });

    const store = useSessionStore.getState();
    expect(store.status).toBe('live');
    expect(store.gameTitle).toBe('Catan');
    expect(store.sessionId).toBe('new-sess-1');

    // Epic #3188 Slice 3: create → go-live sequence, in that order, with the created draft's id.
    expect(createSession).toHaveBeenCalledTimes(1);
    expect(goLive).toHaveBeenCalledTimes(1);
    expect(goLive).toHaveBeenCalledWith('new-sess-1');
    expect(vi.mocked(createSession).mock.invocationCallOrder[0]).toBeLessThan(
      vi.mocked(goLive).mock.invocationCallOrder[0]
    );
  });

  it('startNextGame finalizza la sessione corrente e ne inizia una nuova', async () => {
    const { result } = renderHook(() => useGameNightOrchestrator('night-1'));

    await act(async () => {
      await result.current.startGame({
        gameId: 'game-1',
        gameTitle: 'Catan',
        participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
      });
    });

    // Simula fine partita
    await act(async () => {
      await result.current.startNextGame({
        gameId: 'game-2',
        gameTitle: 'Dixit',
        participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
      });
    });

    const store = useSessionStore.getState();
    expect(store.gameTitle).toBe('Dixit');
    expect(store.currentTurn).toBe(1); // reset per nuovo gioco
  });
});
