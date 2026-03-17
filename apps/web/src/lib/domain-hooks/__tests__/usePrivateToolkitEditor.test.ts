/**
 * usePrivateToolkitEditor Hook Tests (Issue #4980)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';

import { usePrivateToolkitEditor } from '../usePrivateToolkitEditor';
import type { GameToolkitDto } from '../../types/gameToolkit';

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeToolkit(overrides: Partial<GameToolkitDto> = {}): GameToolkitDto {
  return {
    id: 'toolkit-1',
    gameId: null,
    privateGameId: 'priv-abc',
    name: 'My Toolkit',
    version: 1,
    isPublished: false,
    overridesTurnOrder: false,
    overridesScoreboard: false,
    overridesDiceSet: false,
    diceTools: [],
    cardTools: [],
    timerTools: [],
    counterTools: [],
    ...overrides,
  };
}

const mockPrivateGame = { id: 'priv-abc', title: 'My Game', userId: 'user-1' };

/**
 * Mocks `global.fetch` with a sequence of responses.
 * First call = private-game ownership check.
 * Second call = toolkit fetch (by-private-game).
 * Subsequent calls = mutation results.
 */
function mockFetch(responses: Array<{ ok: boolean; status?: number; body?: unknown }>) {
  let callIndex = 0;
  (global.fetch as ReturnType<typeof vi.fn>).mockImplementation(() => {
    const response = responses[callIndex++] ?? { ok: true, body: [] };
    return Promise.resolve({
      ok: response.ok,
      status: response.status ?? (response.ok ? 200 : 500),
      statusText: response.ok ? 'OK' : 'Error',
      json: async () => response.body,
    });
  });
}

// ── Setup ─────────────────────────────────────────────────────────────────────

beforeEach(() => {
  global.fetch = vi.fn();
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('usePrivateToolkitEditor — initial fetch (owner)', () => {
  it('loads toolkit on mount when user owns the game', async () => {
    const tk = makeToolkit();
    mockFetch([
      { ok: true, body: mockPrivateGame }, // ownership check
      { ok: true, body: [tk] }, // toolkit fetch
    ]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));

    expect(result.current.isLoading).toBe(true);
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit).toEqual(tk);
    expect(result.current.isAccessDenied).toBe(false);
    expect(result.current.error).toBeNull();
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/private-games/priv-abc'),
      expect.objectContaining({ credentials: 'include' })
    );
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/game-toolkits/by-private-game/priv-abc'),
      expect.objectContaining({ credentials: 'include' })
    );
  });

  it('returns null toolkit when array is empty (no toolkit yet)', async () => {
    mockFetch([
      { ok: true, body: mockPrivateGame },
      { ok: true, body: [] },
    ]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit).toBeNull();
    expect(result.current.isAccessDenied).toBe(false);
  });

  it('sets isAccessDenied on 403 from private-game endpoint', async () => {
    mockFetch([{ ok: false, status: 403, body: null }]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.isAccessDenied).toBe(true);
    expect(result.current.toolkit).toBeNull();
    expect(result.current.error).toBeNull();
    // Should NOT call toolkit endpoint
    expect(global.fetch).toHaveBeenCalledTimes(1);
  });

  it('sets isAccessDenied on 404 from private-game endpoint', async () => {
    mockFetch([{ ok: false, status: 404, body: null }]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.isAccessDenied).toBe(true);
    expect(global.fetch).toHaveBeenCalledTimes(1);
  });

  it('sets error on non-403/404 HTTP failure', async () => {
    mockFetch([{ ok: false, status: 500, body: null }]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.error).toContain('HTTP 500');
    expect(result.current.isAccessDenied).toBe(false);
  });

  it('picks first toolkit from the array', async () => {
    const tk1 = makeToolkit({ id: 'toolkit-1', version: 2 });
    const tk2 = makeToolkit({ id: 'toolkit-2', version: 1 });
    mockFetch([
      { ok: true, body: mockPrivateGame },
      { ok: true, body: [tk1, tk2] },
    ]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit?.id).toBe('toolkit-1');
  });
});

describe('usePrivateToolkitEditor — createToolkit', () => {
  it('posts with privateGameId (not gameId) and updates state', async () => {
    const created = makeToolkit({ id: 'new-1', name: 'My Toolkit' });
    mockFetch([
      { ok: true, body: mockPrivateGame },
      { ok: true, body: [] }, // initial toolkit fetch (empty)
      { ok: true, body: created }, // create response
    ]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.createToolkit('My Toolkit');
    });

    expect(result.current.toolkit?.name).toBe('My Toolkit');
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/game-toolkits/'),
      expect.objectContaining({ method: 'POST' })
    );
    // Verify privateGameId is in the request body
    const createCall = (global.fetch as ReturnType<typeof vi.fn>).mock.calls[2];
    const body = JSON.parse(createCall[1].body as string) as Record<string, unknown>;
    expect(body.privateGameId).toBe('priv-abc');
    expect(body.gameId).toBeUndefined();
  });

  it('sets error when create fails', async () => {
    mockFetch([
      { ok: true, body: mockPrivateGame },
      { ok: true, body: [] },
      { ok: false, status: 400, body: null },
    ]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.createToolkit('Test').catch(() => {});
    });

    expect(result.current.error).toContain('HTTP 400');
  });
});

describe('usePrivateToolkitEditor — updateOverrides', () => {
  it('calls PUT and updates toolkit', async () => {
    const tk = makeToolkit();
    const updated = makeToolkit({ overridesTurnOrder: true });
    mockFetch([
      { ok: true, body: mockPrivateGame },
      { ok: true, body: [tk] },
      { ok: true, body: updated },
    ]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.updateOverrides({ overridesTurnOrder: true });
    });

    expect(result.current.toolkit?.overridesTurnOrder).toBe(true);
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining(`/api/v1/game-toolkits/${tk.id}`),
      expect.objectContaining({ method: 'PUT' })
    );
  });

  it('throws when no toolkit is loaded', async () => {
    mockFetch([
      { ok: true, body: mockPrivateGame },
      { ok: true, body: [] },
    ]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await expect(
      act(async () => {
        await result.current.updateOverrides({ overridesTurnOrder: true });
      })
    ).rejects.toThrow('No toolkit loaded');
  });
});

describe('usePrivateToolkitEditor — tool CRUD', () => {
  it('addDiceTool calls the dice-tools endpoint', async () => {
    const tk = makeToolkit();
    const withDice = makeToolkit({
      diceTools: [{ name: '2×d6', diceType: 'd6', quantity: 2, isInteractive: true }],
    });
    mockFetch([
      { ok: true, body: mockPrivateGame },
      { ok: true, body: [tk] },
      { ok: true, body: withDice },
    ]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.addDiceTool({
        name: '2×d6',
        diceType: 'd6',
        quantity: 2,
        isInteractive: true,
      });
    });

    expect(result.current.toolkit?.diceTools).toHaveLength(1);
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/dice-tools'),
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('removeDiceTool calls DELETE', async () => {
    const tk = makeToolkit({
      diceTools: [{ name: '2×d6', diceType: 'd6', quantity: 2, isInteractive: true }],
    });
    const without = makeToolkit({ diceTools: [] });
    mockFetch([
      { ok: true, body: mockPrivateGame },
      { ok: true, body: [tk] },
      { ok: true, body: without },
    ]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.removeDiceTool('2×d6');
    });

    expect(result.current.toolkit?.diceTools).toHaveLength(0);
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/dice-tools/'),
      expect.objectContaining({ method: 'DELETE' })
    );
  });

  it('addCounterTool calls the counter-tools endpoint', async () => {
    const tk = makeToolkit();
    const withCounter = makeToolkit({
      counterTools: [
        { name: 'Risorse', minValue: 0, maxValue: 100, defaultValue: 0, isPerPlayer: true },
      ],
    });
    mockFetch([
      { ok: true, body: mockPrivateGame },
      { ok: true, body: [tk] },
      { ok: true, body: withCounter },
    ]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.addCounterTool({
        name: 'Risorse',
        minValue: 0,
        maxValue: 100,
        defaultValue: 0,
        isPerPlayer: true,
      });
    });

    expect(result.current.toolkit?.counterTools).toHaveLength(1);
  });
});

describe('usePrivateToolkitEditor — no publish function', () => {
  it('does not expose a publish function', () => {
    mockFetch([
      { ok: true, body: mockPrivateGame },
      { ok: true, body: [] },
    ]);

    const { result } = renderHook(() => usePrivateToolkitEditor('priv-abc'));
    // No publish method exposed (private toolkits are private, no publish concept)
    expect('publish' in result.current).toBe(false);
  });
});
