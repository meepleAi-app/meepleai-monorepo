import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useGameNightOrchestrator } from '../useGameNightOrchestrator';
import { createSession, goLive, finalizeSession } from '@/lib/api/clients/gameSessionsClient';
import { ConflictError } from '@/lib/api/core/errors';
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
    // Reset implementations too so per-test *Once overrides don't leak between tests.
    vi.mocked(createSession).mockReset();
    vi.mocked(goLive).mockReset();
    vi.mocked(finalizeSession).mockReset();
    vi.mocked(createSession).mockResolvedValue({ sessionId: 'new-sess-1', code: 'ABC-123' });
    vi.mocked(finalizeSession).mockResolvedValue(undefined);
    vi.mocked(goLive).mockResolvedValue({
      sessionId: 'new-sess-1',
      gameNightId: 'night-1',
      gameNightSessionId: 'gns-1',
      playOrder: 1,
      status: 'InProgress',
    });
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

  it('riprova lo stesso gioco dopo un go-live fallito: riusa il draft (createSession una sola volta)', async () => {
    // Issue #3217: go-live fallisce alla prima, poi riesce. Il draft NON deve essere ricreato.
    vi.mocked(goLive).mockRejectedValueOnce(new Error('network blip'));

    const { result } = renderHook(() => useGameNightOrchestrator('night-1'));
    const payload = {
      gameId: 'game-1',
      gameTitle: 'Catan',
      participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
    };

    // 1° tentativo: go-live rigetta → il draft resta "pending" (nessun orfano ricreato dopo)
    await act(async () => {
      await expect(result.current.startGame(payload)).rejects.toThrow('network blip');
    });

    // 2° tentativo: stesso gioco → riusa il draft, go-live riesce, store live
    await act(async () => {
      await result.current.startGame(payload);
    });

    // Il draft è stato creato UNA SOLA VOLTA (nessun orfano accumulato)
    expect(createSession).toHaveBeenCalledTimes(1);
    // go-live è stato invocato due volte (fail + retry self-healing)
    expect(goLive).toHaveBeenCalledTimes(2);
    // Nessuna compensazione: stesso gioco → nessun finalize del draft
    expect(finalizeSession).not.toHaveBeenCalled();

    const store = useSessionStore.getState();
    expect(store.status).toBe('live');
    expect(store.sessionId).toBe('new-sess-1');
  });

  it('nessun accumulo di orfani su double-tap: createSession al massimo una volta', async () => {
    const { result } = renderHook(() => useGameNightOrchestrator('night-1'));
    const payload = {
      gameId: 'game-1',
      gameTitle: 'Catan',
      participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
    };

    // Due chiamate ravvicinate (double-tap): la seconda deve essere bloccata dal guard.
    await act(async () => {
      const first = result.current.startGame(payload);
      const second = result.current.startGame(payload);
      await Promise.all([first, second]);
    });

    expect(vi.mocked(createSession).mock.calls.length).toBeLessThanOrEqual(1);
    expect(createSession).toHaveBeenCalledTimes(1);

    const store = useSessionStore.getState();
    expect(store.status).toBe('live');
  });

  it('gioco diverso dopo un fallimento: compensa il draft abbandonato e ne crea uno nuovo', async () => {
    // Draft per il gioco A
    vi.mocked(createSession)
      .mockResolvedValueOnce({ sessionId: 'A-id', code: 'AAA-111' })
      .mockResolvedValueOnce({ sessionId: 'B-id', code: 'BBB-222' });
    // go-live del gioco A fallisce; quello del gioco B (default) riesce
    vi.mocked(goLive).mockRejectedValueOnce(new Error('go-live A failed'));

    const { result } = renderHook(() => useGameNightOrchestrator('night-1'));

    // Tentativo gioco A → go-live fallisce, draft 'A-id' resta pending
    await act(async () => {
      await expect(
        result.current.startGame({
          gameId: 'game-A',
          gameTitle: 'Catan',
          participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
        })
      ).rejects.toThrow('go-live A failed');
    });

    // Tentativo gioco B → compensa 'A-id', crea draft 'B-id'
    await act(async () => {
      await result.current.startGame({
        gameId: 'game-B',
        gameTitle: 'Dixit',
        participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
      });
    });

    // Compensazione del draft abbandonato del gioco A
    expect(finalizeSession).toHaveBeenCalledWith('A-id');
    // Due draft creati (A abbandonato, B nuovo)
    expect(createSession).toHaveBeenCalledTimes(2);

    const store = useSessionStore.getState();
    expect(store.status).toBe('live');
    expect(store.sessionId).toBe('B-id');
    expect(store.gameTitle).toBe('Dixit');
  });

  it('mantiene lo stato di errore ConflictError e rilancia', async () => {
    vi.mocked(goLive).mockRejectedValueOnce(new ConflictError({ message: 'already live' }));

    const { result } = renderHook(() => useGameNightOrchestrator('night-1'));

    await act(async () => {
      await expect(
        result.current.startGame({
          gameId: 'game-1',
          gameTitle: 'Catan',
          participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
        })
      ).rejects.toBeInstanceOf(ConflictError);
    });

    expect(result.current.error).toBe(
      'Una partita è già attiva per questa serata. Finalizzala prima di iniziarne una nuova.'
    );
  });

  it('mantiene lo stato di errore generico e rilancia', async () => {
    vi.mocked(goLive).mockRejectedValueOnce(new Error('boom'));

    const { result } = renderHook(() => useGameNightOrchestrator('night-1'));

    await act(async () => {
      await expect(
        result.current.startGame({
          gameId: 'game-1',
          gameTitle: 'Catan',
          participants: [{ id: 'p1', displayName: 'Marco', isGuest: false }],
        })
      ).rejects.toThrow('boom');
    });

    expect(result.current.error).toBe('Impossibile avviare la partita. Riprova.');
  });
});
