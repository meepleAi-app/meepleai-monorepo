/**
 * useWhiteboardTool Hook Tests (Issue #4977)
 *
 * Coverage:
 * - Initial load: fetches whiteboard state on mount
 * - 404 handling: initializes with empty defaults
 * - Error handling: sets error state, falls back to empty state
 * - saveStrokes: debounced 500ms, calls correct endpoint
 * - saveStructured: debounced 500ms, calls correct endpoint
 * - clear: immediate, resets strokes and tokens
 * - applySSEEvent: stroke-added, structured-updated, whiteboard-cleared
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';

import { useWhiteboardTool } from '../useWhiteboardTool';
import type { WhiteboardState, WhiteboardSSEEvent, Stroke } from '@/components/session/types';

// ── Fixtures ──────────────────────────────────────────────────────────────────

const SESSION_ID = 'sess-abc';
const API_BASE = 'http://localhost:8080';

const MOCK_STATE: WhiteboardState = {
  strokes: [],
  tokens: [{ id: 'tok-1', color: '#ef4444', label: 'A', gridX: 0, gridY: 0 }],
  gridSize: '4x4',
  showGrid: true,
  mode: 'both',
};

const MOCK_STROKE: Stroke = {
  id: 'stroke-1',
  points: [
    { x: 0, y: 0 },
    { x: 10, y: 10 },
  ],
  color: '#000000',
  thickness: 4,
  isEraser: false,
};

// ── Setup / helpers ───────────────────────────────────────────────────────────

let fetchMock: ReturnType<typeof vi.fn>;

/** Render hook and wait for initial load to complete */
async function renderAndLoad() {
  const hook = renderHook(() => useWhiteboardTool({ sessionId: SESSION_ID, apiBaseUrl: API_BASE }));
  await waitFor(() => expect(hook.result.current.isLoading).toBe(false));
  return hook;
}

// ── Initial load ──────────────────────────────────────────────────────────────

describe('initial load', () => {
  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => MOCK_STATE,
    });
    global.fetch = fetchMock;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('starts with null state before fetch resolves', () => {
    const { result } = renderHook(() =>
      useWhiteboardTool({ sessionId: SESSION_ID, apiBaseUrl: API_BASE })
    );
    // On first render, state is null (async load not yet done)
    expect(result.current.whiteboardState).toBeNull();
  });

  it('loads whiteboard state from API', async () => {
    const { result } = await renderAndLoad();
    expect(result.current.whiteboardState).toEqual(MOCK_STATE);
    expect(result.current.error).toBeNull();
    expect(result.current.isLoading).toBe(false);
  });

  it('calls GET /api/v1/live-sessions/{sessionId}/whiteboard', async () => {
    await renderAndLoad();
    expect(fetchMock).toHaveBeenCalledWith(
      `${API_BASE}/api/v1/live-sessions/${SESSION_ID}/whiteboard`,
      expect.objectContaining({ credentials: 'include' })
    );
  });
});

// ── 404 handling ──────────────────────────────────────────────────────────────

describe('404 handling', () => {
  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValueOnce({ ok: false, status: 404, statusText: 'Not Found' });
    global.fetch = fetchMock;
  });

  afterEach(() => vi.restoreAllMocks());

  it('initializes with empty defaults when 404', async () => {
    const { result } = await renderAndLoad();
    expect(result.current.whiteboardState).toEqual({
      strokes: [],
      tokens: [],
      gridSize: '4x4',
      showGrid: true,
      mode: 'both',
    });
    expect(result.current.error).toBeNull();
  });
});

// ── Error handling ────────────────────────────────────────────────────────────

describe('error handling', () => {
  beforeEach(() => {
    fetchMock = vi.fn().mockRejectedValueOnce(new Error('Network error'));
    global.fetch = fetchMock;
  });

  afterEach(() => vi.restoreAllMocks());

  it('sets error and falls back to empty state on fetch failure', async () => {
    const { result } = await renderAndLoad();
    expect(result.current.error).toBe('Network error');
    expect(result.current.whiteboardState).toEqual({
      strokes: [],
      tokens: [],
      gridSize: '4x4',
      showGrid: true,
      mode: 'both',
    });
  });
});

// ── saveStrokes (debounced) ───────────────────────────────────────────────────

describe('saveStrokes', () => {
  beforeEach(() => {
    // shouldAdvanceTime: true lets waitFor work even with fake timers
    vi.useFakeTimers({ shouldAdvanceTime: true });
    fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, status: 200, json: async () => MOCK_STATE })
      .mockResolvedValue({ ok: true, status: 200, json: async () => ({}) });
    global.fetch = fetchMock;
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('does not call endpoint immediately (debounced)', async () => {
    const { result } = renderHook(() =>
      useWhiteboardTool({ sessionId: SESSION_ID, apiBaseUrl: API_BASE })
    );
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => {
      result.current.saveStrokes([MOCK_STROKE]);
    });

    // Advance 400ms – not yet fired
    act(() => {
      vi.advanceTimersByTime(400);
    });
    expect(fetchMock).toHaveBeenCalledTimes(1); // only initial load
  });

  it('calls strokes endpoint after 500ms', async () => {
    const { result } = renderHook(() =>
      useWhiteboardTool({ sessionId: SESSION_ID, apiBaseUrl: API_BASE })
    );
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => {
      result.current.saveStrokes([MOCK_STROKE]);
    });
    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${API_BASE}/api/v1/live-sessions/${SESSION_ID}/whiteboard/strokes`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ strokes: [MOCK_STROKE] }),
        })
      )
    );
  });

  it('debounces: rapid calls only trigger one request', async () => {
    const { result } = renderHook(() =>
      useWhiteboardTool({ sessionId: SESSION_ID, apiBaseUrl: API_BASE })
    );
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => {
      result.current.saveStrokes([]);
      result.current.saveStrokes([MOCK_STROKE]);
    });
    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    // Should only have 2 fetch calls: 1 load + 1 debounced save
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
  });
});

// ── saveStructured (debounced) ────────────────────────────────────────────────

describe('saveStructured', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, status: 200, json: async () => MOCK_STATE })
      .mockResolvedValue({ ok: true, status: 200, json: async () => ({}) });
    global.fetch = fetchMock;
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('calls structured endpoint after 500ms', async () => {
    const { result } = renderHook(() =>
      useWhiteboardTool({ sessionId: SESSION_ID, apiBaseUrl: API_BASE })
    );
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => {
      result.current.saveStructured([], '6x6', false, 'structured');
    });
    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        `${API_BASE}/api/v1/live-sessions/${SESSION_ID}/whiteboard/structured`,
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({
            tokens: [],
            gridSize: '6x6',
            showGrid: false,
            mode: 'structured',
          }),
        })
      )
    );
  });
});

// ── clear ─────────────────────────────────────────────────────────────────────

describe('clear', () => {
  beforeEach(() => {
    fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, status: 200, json: async () => MOCK_STATE })
      .mockResolvedValue({ ok: true, status: 200, json: async () => ({}) });
    global.fetch = fetchMock;
  });

  afterEach(() => vi.restoreAllMocks());

  it('calls clear endpoint', async () => {
    const { result } = await renderAndLoad();
    await act(async () => {
      await result.current.clear();
    });
    expect(fetchMock).toHaveBeenCalledWith(
      `${API_BASE}/api/v1/live-sessions/${SESSION_ID}/whiteboard/clear`,
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('clears strokes and tokens in local state', async () => {
    const stateWithData: WhiteboardState = {
      ...MOCK_STATE,
      strokes: [MOCK_STROKE],
      tokens: [{ id: 't1', color: '#ef4444', label: 'A', gridX: 0, gridY: 0 }],
    };
    fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, status: 200, json: async () => stateWithData })
      .mockResolvedValue({ ok: true, status: 200, json: async () => ({}) });
    global.fetch = fetchMock;

    const { result } = renderHook(() =>
      useWhiteboardTool({ sessionId: SESSION_ID, apiBaseUrl: API_BASE })
    );
    await waitFor(() => expect(result.current.whiteboardState?.tokens).toHaveLength(1));

    await act(async () => {
      await result.current.clear();
    });

    expect(result.current.whiteboardState?.strokes).toHaveLength(0);
    expect(result.current.whiteboardState?.tokens).toHaveLength(0);
  });

  it('throws and sets error when clear fails', async () => {
    fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, status: 200, json: async () => MOCK_STATE })
      .mockResolvedValueOnce({ ok: false, status: 500, statusText: 'Internal Server Error' });
    global.fetch = fetchMock;

    const { result } = await renderAndLoad();

    let caughtError: unknown;
    await act(async () => {
      try {
        await result.current.clear();
      } catch (e) {
        caughtError = e;
      }
    });

    expect(caughtError).toBeInstanceOf(Error);
    expect(result.current.error).toMatch(/HTTP 500/);
  });
});

// ── applySSEEvent ─────────────────────────────────────────────────────────────

describe('applySSEEvent', () => {
  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => MOCK_STATE,
    });
    global.fetch = fetchMock;
  });

  afterEach(() => vi.restoreAllMocks());

  it('stroke-added appends stroke to strokes array', async () => {
    const { result } = await renderAndLoad();

    const event: WhiteboardSSEEvent = { type: 'stroke-added', stroke: MOCK_STROKE };
    act(() => {
      result.current.applySSEEvent(event);
    });

    expect(result.current.whiteboardState?.strokes).toHaveLength(1);
    expect(result.current.whiteboardState?.strokes[0]).toEqual(MOCK_STROKE);
  });

  it('structured-updated replaces tokens and settings', async () => {
    const { result } = await renderAndLoad();

    const newTokens = [{ id: 'tok-99', color: '#0ea5e9', label: 'X', gridX: 2, gridY: 2 }];
    const event: WhiteboardSSEEvent = {
      type: 'structured-updated',
      tokens: newTokens,
      gridSize: '6x6',
      showGrid: false,
      mode: 'structured',
    };
    act(() => {
      result.current.applySSEEvent(event);
    });

    expect(result.current.whiteboardState?.tokens).toEqual(newTokens);
    expect(result.current.whiteboardState?.gridSize).toBe('6x6');
    expect(result.current.whiteboardState?.showGrid).toBe(false);
  });

  it('whiteboard-cleared resets strokes and tokens', async () => {
    const stateWithData: WhiteboardState = {
      ...MOCK_STATE,
      strokes: [MOCK_STROKE],
      tokens: [{ id: 't1', color: '#ef4444', label: 'A', gridX: 0, gridY: 0 }],
    };
    fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => stateWithData,
    });
    global.fetch = fetchMock;

    const { result } = renderHook(() =>
      useWhiteboardTool({ sessionId: SESSION_ID, apiBaseUrl: API_BASE })
    );
    await waitFor(() => expect(result.current.whiteboardState?.strokes).toHaveLength(1));

    const event: WhiteboardSSEEvent = { type: 'whiteboard-cleared' };
    act(() => {
      result.current.applySSEEvent(event);
    });

    expect(result.current.whiteboardState?.strokes).toHaveLength(0);
    expect(result.current.whiteboardState?.tokens).toHaveLength(0);
  });

  it('stroke-added is a no-op when state is null', () => {
    const { result } = renderHook(() =>
      useWhiteboardTool({ sessionId: SESSION_ID, apiBaseUrl: API_BASE })
    );
    // State is null before load completes
    expect(result.current.whiteboardState).toBeNull();

    const event: WhiteboardSSEEvent = { type: 'stroke-added', stroke: MOCK_STROKE };
    act(() => {
      result.current.applySSEEvent(event);
    });

    expect(result.current.whiteboardState).toBeNull();
  });
});
