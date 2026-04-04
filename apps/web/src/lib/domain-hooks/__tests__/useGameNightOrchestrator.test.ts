import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useGameNightOrchestrator } from '../useGameNightOrchestrator';
import { useSessionStore } from '@/stores/session/store';

// Mock API call
vi.mock('@/lib/api/clients/gameSessionsClient', () => ({
  createSession: vi.fn().mockResolvedValue({ sessionId: 'new-sess-1', code: 'ABC-123' }),
  finalizeSession: vi.fn().mockResolvedValue(undefined),
}));

describe('useGameNightOrchestrator', () => {
  beforeEach(() => {
    localStorage.clear();
    useSessionStore.getState().reset();
  });

  it('startGame crea una sessione e aggiorna lo store', async () => {
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
