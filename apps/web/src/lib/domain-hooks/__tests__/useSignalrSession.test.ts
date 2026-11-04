/**
 * use-signalr-session Tests
 *
 * Game Night Improvvisata — Task 13
 *
 * Unit tests for the Zustand live-session-store and the useSignalRSession hook.
 * Full SignalR integration is validated in E2E tests.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';

// ──────────────────────────────────────────────────────────
// Mock @microsoft/signalr before importing the hook
// ──────────────────────────────────────────────────────────

const mockOn = vi.fn();
const mockStart = vi.fn().mockResolvedValue(undefined);
const mockStop = vi.fn().mockResolvedValue(undefined);
const mockInvoke = vi.fn().mockResolvedValue(undefined);
const mockOnreconnected = vi.fn();
const mockOnreconnecting = vi.fn();
const mockOnclose = vi.fn();

const mockConnection = {
  on: mockOn,
  start: mockStart,
  stop: mockStop,
  invoke: mockInvoke,
  onreconnected: mockOnreconnected,
  onreconnecting: mockOnreconnecting,
  onclose: mockOnclose,
  state: 'Connected',
};

const mockWithUrl = vi.fn().mockReturnThis();
const mockWithAutomaticReconnect = vi.fn().mockReturnThis();
const mockConfigureLogging = vi.fn().mockReturnThis();
const mockBuild = vi.fn().mockReturnValue(mockConnection);

// #1972 M4 batch: vitest v4 rejects arrow-mock used with `new`. Regular function
// (not arrow) is `new`-compatible because `this` is bound. Class approach fails here
// due to hoisted `vi.mock` factory referencing spy consts before initialization.
vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: vi.fn().mockImplementation(function (this: {
    withUrl: () => unknown;
    withAutomaticReconnect: () => unknown;
    configureLogging: () => unknown;
    build: () => unknown;
  }) {
    this.withUrl = mockWithUrl;
    this.withAutomaticReconnect = mockWithAutomaticReconnect;
    this.configureLogging = mockConfigureLogging;
    this.build = mockBuild;
  }),
  HubConnectionState: {
    Connected: 'Connected',
    Connecting: 'Connecting',
    Disconnected: 'Disconnected',
    Disconnecting: 'Disconnecting',
    Reconnecting: 'Reconnecting',
  },
  LogLevel: {
    Warning: 1,
  },
}));

// ──────────────────────────────────────────────────────────
// Imports (after mock setup)
// ──────────────────────────────────────────────────────────

import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { useSignalRSession } from '../useSignalrSession';

// ──────────────────────────────────────────────────────────
// Store tests
// ──────────────────────────────────────────────────────────

describe('useLiveSessionStore', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().reset();
    vi.clearAllMocks();
  });

  it('setSession updates state fields', () => {
    useLiveSessionStore.getState().setSession({
      sessionId: 'sess-1',
      gameName: 'Catan',
      status: 'InProgress',
    });

    const state = useLiveSessionStore.getState();
    expect(state.sessionId).toBe('sess-1');
    expect(state.gameName).toBe('Catan');
    expect(state.status).toBe('InProgress');
  });

  it('addDispute appends to the disputes array', () => {
    const dispute = {
      id: 'd-1',
      description: 'Can Robber be placed on desert?',
      verdict: 'No — desert is exempt.',
      ruleReferences: ['Catan 4.2'],
      raisedByPlayerName: 'Alice',
      timestamp: '2026-03-15T10:00:00Z',
    };

    useLiveSessionStore.getState().addDispute(dispute);
    const { disputes } = useLiveSessionStore.getState();
    expect(disputes).toHaveLength(1);
    expect(disputes[0]).toEqual(dispute);
  });

  it('reset clears all state to initial values', () => {
    // Dirty the store
    useLiveSessionStore.getState().setSession({ sessionId: 'sess-1', gameName: 'Test' });
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 100 }] },
    });
    useLiveSessionStore.getState().addProposal({
      id: 'p-1',
      playerName: 'Alice',
      delta: 5,
      timestamp: Date.now(),
    });
    useLiveSessionStore.getState().setConnected(true);

    useLiveSessionStore.getState().reset();

    const state = useLiveSessionStore.getState();
    expect(state.sessionId).toBeNull();
    expect(state.gameName).toBe('');
    expect(state.scoringType).toBeNull();
    expect(state.scoreData).toBeNull();
    expect(state.pendingProposals).toEqual([]);
    expect(state.disputes).toEqual([]);
    expect(state.isConnected).toBe(false);
  });
});

// ──────────────────────────────────────────────────────────
// Hook tests
// ──────────────────────────────────────────────────────────

describe('useSignalRSession', () => {
  const sessionId = 'test-session-123';

  beforeEach(() => {
    useLiveSessionStore.getState().reset();
    vi.clearAllMocks();
    // Reset start mock to succeed by default
    mockStart.mockResolvedValue(undefined);
    mockStop.mockResolvedValue(undefined);
    mockInvoke.mockResolvedValue(undefined);
    mockConnection.state = 'Connected';
  });

  it('connects to SignalR hub on mount', async () => {
    const { unmount } = renderHook(() => useSignalRSession(sessionId));

    // Allow async start() to settle
    await act(async () => {
      await new Promise(r => setTimeout(r, 0));
    });

    expect(mockBuild).toHaveBeenCalled();
    expect(mockStart).toHaveBeenCalled();
    expect(mockInvoke).toHaveBeenCalledWith('JoinSession', sessionId);

    unmount();
  });

  // 🔴 #4059 — l'asserzione che mancava, ed e' il motivo per cui il difetto e' vissuto a lungo.
  //
  // Questo file sostituisce `HubConnectionBuilder` per intero: `mockWithUrl` e' una spia di cui
  // nessun test asseriva l'argomento. Il test sopra («connects to SignalR hub on mount») passava
  // con `withUrl('/hubs/game-state')`, cioe' con un URL che risponde 404 su entrambe le origini
  // — non PUO' osservare un URL sbagliato, perche' il mock non lo usa per connettersi.
  //
  // Le due asserzioni corrispondono ai due difetti misurati, e nessuna delle due e' implicata
  // dall'altra:
  //   - il path era `/hubs/game-state`, col trattino; il backend mappa `/hubs/gamestate`.
  //   - l'URL era relativo, quindi risolveva su :3000, dove non esiste proxy per `/hubs`.
  //
  // Volutamente non si asserisce l'host esatto: dipende da `NEXT_PUBLIC_API_BASE`, che cambia
  // fra dev, CI e staging. Cio' che deve restare vero e' che sia ASSOLUTO.
  it('si connette a un URL assoluto e al path che il backend mappa', async () => {
    const { unmount } = renderHook(() => useSignalRSession(sessionId));

    await act(async () => {
      await new Promise(r => setTimeout(r, 0));
    });

    expect(mockWithUrl).toHaveBeenCalledTimes(1);
    const [url, options] = mockWithUrl.mock.calls[0] as [string, { withCredentials?: boolean }];

    expect(url).toMatch(/^https?:\/\//);
    expect(url.endsWith('/hubs/gamestate')).toBe(true);
    // Il trattino e' il path che non esiste: va escluso esplicitamente, perche'
    // `endsWith('/hubs/gamestate')` da solo e' falso anche per `/hubs/game-state` ma un
    // refactoring potrebbe reintrodurlo altrove nella stringa.
    expect(url).not.toContain('game-state');

    // Cross-origin verso :8080 con cookie. ⚠️ L'effetto NON e' che senza questo la connessione
    // fallisce: `GameStateHub` porta `[AllowAnonymous]` (supporto guest, E3-4), quindi senza
    // cookie si connetterebbe comunque — come anonimo. L'effetto e' peggiore di un fallimento:
    // `SessionParticipantIdProvider` deriva l'identita' dal principal, quindi un utente
    // autenticato diventerebbe un guest in silenzio, e gli eventi indirizzati al suo userId non
    // arriverebbero. E' per questo che l'opzione va asserita e non lasciata al caso.
    expect(options?.withCredentials).toBe(true);

    unmount();
  });

  it('does not connect when sessionId is null', () => {
    renderHook(() => useSignalRSession(null));
    expect(mockBuild).not.toHaveBeenCalled();
  });

  it('cleans up (stops) on unmount', async () => {
    const { unmount } = renderHook(() => useSignalRSession(sessionId));

    await act(async () => {
      await new Promise(r => setTimeout(r, 0));
    });

    unmount();

    await act(async () => {
      await new Promise(r => setTimeout(r, 0));
    });

    expect(mockStop).toHaveBeenCalled();
  });
});

// ──────────────────────────────────────────────────────────
// Block A #2389 — ScoringConfigured consumer
// ──────────────────────────────────────────────────────────

describe('useSignalRSession — Block A #2389 ScoringConfigured', () => {
  const sessionId = 'session-123';

  beforeEach(() => {
    useLiveSessionStore.getState().reset();
    vi.clearAllMocks();
    mockStart.mockResolvedValue(undefined);
    mockStop.mockResolvedValue(undefined);
    mockInvoke.mockResolvedValue(undefined);
    mockConnection.state = 'Connected';
  });

  /**
   * Helper: locate the handler registered for a given hub event name.
   * Mirrors the way tests in this file inspect `mockOn` after the hook mounts.
   */
  function getRegisteredHandler(eventName: string): ((payload: unknown) => void) | undefined {
    const call = mockOn.mock.calls.find(c => c[0] === eventName);
    return call?.[1] as ((payload: unknown) => void) | undefined;
  }

  it('calls store.setScoringConfig when receiving ScoringConfigured event', async () => {
    const { unmount } = renderHook(() => useSignalRSession(sessionId));

    await act(async () => {
      await new Promise(r => setTimeout(r, 0));
    });

    const handler = getRegisteredHandler('ScoringConfigured');
    expect(handler).toBeDefined();

    const payload = {
      sessionId,
      scoringType: 'Points' as const,
      scoreData: JSON.stringify({ scores: [{ playerId: 'p1', points: 3 }] }),
    };

    act(() => {
      handler!(payload);
    });

    const state = useLiveSessionStore.getState();
    expect(state.scoringType).toBe('Points');
    expect(state.scoreData).toEqual({ scores: [{ playerId: 'p1', points: 3 }] });

    unmount();
  });

  it('does not crash and leaves store untouched when scoreData is malformed JSON', async () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});

    const { unmount } = renderHook(() => useSignalRSession(sessionId));

    await act(async () => {
      await new Promise(r => setTimeout(r, 0));
    });

    const handler = getRegisteredHandler('ScoringConfigured');
    expect(handler).toBeDefined();

    const badPayload = {
      sessionId,
      scoringType: 'Points' as const,
      scoreData: '{not valid json',
    };

    act(() => {
      handler!(badPayload);
    });

    const state = useLiveSessionStore.getState();
    expect(state.scoringType).toBeNull();
    expect(state.scoreData).toBeNull();
    expect(warnSpy).toHaveBeenCalled();

    warnSpy.mockRestore();
    unmount();
  });
});
