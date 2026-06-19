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

  it('updateScore updates score for the given player', () => {
    useLiveSessionStore.getState().updateScore('Alice', 42);
    useLiveSessionStore.getState().updateScore('Bob', 18);

    const { scores } = useLiveSessionStore.getState();
    expect(scores['Alice']).toBe(42);
    expect(scores['Bob']).toBe(18);
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
    useLiveSessionStore.getState().updateScore('Alice', 100);
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
    expect(state.scores).toEqual({});
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
