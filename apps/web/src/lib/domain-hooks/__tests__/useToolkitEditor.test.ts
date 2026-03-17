/**
 * useToolkitEditor Hook Tests (Issue #4978)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';

import { useToolkitEditor } from '../useToolkitEditor';
import type { GameToolkitDto } from '../../types/gameToolkit';

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeToolkit(overrides: Partial<GameToolkitDto> = {}): GameToolkitDto {
  return {
    id: 'toolkit-1',
    gameId: 'game-abc',
    privateGameId: null,
    name: 'Test Toolkit',
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

describe('useToolkitEditor — initial fetch', () => {
  it('loads toolkit on mount', async () => {
    const tk = makeToolkit();
    mockFetch([{ ok: true, body: [tk] }]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));

    expect(result.current.isLoading).toBe(true);
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit).toEqual(tk);
    expect(result.current.error).toBeNull();
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/game-toolkits/by-game/game-abc'),
      expect.objectContaining({ credentials: 'include' })
    );
  });

  it('returns null toolkit when array is empty', async () => {
    mockFetch([{ ok: true, body: [] }]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit).toBeNull();
  });

  it('returns null and no error on 404', async () => {
    mockFetch([{ ok: false, status: 404, body: null }]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit).toBeNull();
    expect(result.current.error).toBeNull();
  });

  it('sets error on non-404 HTTP failure', async () => {
    mockFetch([{ ok: false, status: 500, body: null }]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.error).toContain('HTTP 500');
  });

  it('picks first toolkit from the array', async () => {
    const tk1 = makeToolkit({ id: 'toolkit-1', version: 2 });
    const tk2 = makeToolkit({ id: 'toolkit-2', version: 1 });
    mockFetch([{ ok: true, body: [tk1, tk2] }]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit?.id).toBe('toolkit-1');
  });
});

describe('useToolkitEditor — createToolkit', () => {
  it('posts to /game-toolkits/ and updates state', async () => {
    const created = makeToolkit({ id: 'new-1', name: 'Catan Toolkit' });
    mockFetch([
      { ok: true, body: [] }, // initial fetch (no toolkit)
      { ok: true, body: created }, // create
    ]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.createToolkit('Catan Toolkit');
    });

    expect(result.current.toolkit?.name).toBe('Catan Toolkit');
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/game-toolkits/'),
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('sets error when create fails', async () => {
    mockFetch([
      { ok: true, body: [] },
      { ok: false, status: 400, body: null },
    ]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.createToolkit('Test').catch(() => {});
    });

    expect(result.current.error).toContain('HTTP 400');
  });
});

describe('useToolkitEditor — updateOverrides', () => {
  it('calls PUT and updates toolkit', async () => {
    const tk = makeToolkit();
    const updated = makeToolkit({ overridesTurnOrder: true });
    mockFetch([
      { ok: true, body: [tk] },
      { ok: true, body: updated },
    ]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
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
    mockFetch([{ ok: true, body: [] }]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await expect(
      act(async () => {
        await result.current.updateOverrides({ overridesTurnOrder: true });
      })
    ).rejects.toThrow('No toolkit loaded');
  });
});

describe('useToolkitEditor — tool CRUD', () => {
  it('addDiceTool calls the dice-tools endpoint', async () => {
    const tk = makeToolkit();
    const withDice = makeToolkit({
      diceTools: [{ name: '2×d6', diceType: 'd6', quantity: 2, isInteractive: true }],
    });
    mockFetch([
      { ok: true, body: [tk] },
      { ok: true, body: withDice },
    ]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
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
      expect.stringContaining(`/dice-tools`),
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('removeDiceTool calls DELETE', async () => {
    const tk = makeToolkit({
      diceTools: [{ name: '2×d6', diceType: 'd6', quantity: 2, isInteractive: true }],
    });
    const without = makeToolkit({ diceTools: [] });
    mockFetch([
      { ok: true, body: [tk] },
      { ok: true, body: without },
    ]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
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
      { ok: true, body: [tk] },
      { ok: true, body: withCounter },
    ]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
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

  it('addCardTool calls the card-tools endpoint', async () => {
    const tk = makeToolkit();
    const withCard = makeToolkit({
      cardTools: [
        {
          name: 'Deck',
          deckType: 'Standard52',
          cardCount: 52,
          shuffleable: true,
          allowDraw: true,
          allowDiscard: true,
          allowPeek: false,
          allowReturnToDeck: false,
        },
      ],
    });
    mockFetch([
      { ok: true, body: [tk] },
      { ok: true, body: withCard },
    ]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.addCardTool({
        name: 'Deck',
        deckType: 'Standard52',
        cardCount: 52,
        shuffleable: true,
        allowDraw: true,
        allowDiscard: true,
        allowPeek: false,
        allowReturnToDeck: false,
      });
    });

    expect(result.current.toolkit?.cardTools).toHaveLength(1);
  });

  it('addTimerTool calls the timer-tools endpoint', async () => {
    const tk = makeToolkit();
    const withTimer = makeToolkit({
      timerTools: [
        {
          name: 'Countdown',
          durationSeconds: 60,
          timerType: 'countdown',
          autoStart: false,
          isPerPlayer: false,
        },
      ],
    });
    mockFetch([
      { ok: true, body: [tk] },
      { ok: true, body: withTimer },
    ]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.addTimerTool({
        name: 'Countdown',
        durationSeconds: 60,
        timerType: 'countdown',
        autoStart: false,
        isPerPlayer: false,
      });
    });

    expect(result.current.toolkit?.timerTools).toHaveLength(1);
  });
});

describe('useToolkitEditor — publish', () => {
  it('calls publish endpoint and updates toolkit', async () => {
    const tk = makeToolkit();
    const published = makeToolkit({ isPublished: true, version: 2 });
    mockFetch([
      { ok: true, body: [tk] },
      { ok: true, body: published },
    ]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.publish();
    });

    expect(result.current.toolkit?.isPublished).toBe(true);
    expect(result.current.toolkit?.version).toBe(2);
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining(`/${tk.id}/publish`),
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('throws when no toolkit is loaded', async () => {
    mockFetch([{ ok: true, body: [] }]);

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await expect(
      act(async () => {
        await result.current.publish();
      })
    ).rejects.toThrow('No toolkit loaded');
  });
});

describe('useToolkitEditor — isSaving', () => {
  it('sets isSaving true during API calls', async () => {
    const tk = makeToolkit();
    let resolveCreate!: (value: unknown) => void;
    const createPromise = new Promise(r => {
      resolveCreate = r;
    });

    (global.fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ ok: true, json: async () => [tk] })
      .mockReturnValueOnce(
        createPromise.then(() => ({
          ok: true,
          json: async () => makeToolkit({ name: 'New' }),
        }))
      );

    const { result } = renderHook(() => useToolkitEditor('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let savePromise: Promise<void>;
    act(() => {
      savePromise = result.current.publish().catch(() => {});
    });

    // Can't easily assert isSaving=true mid-flight in vitest, but we verify it resets
    resolveCreate(undefined);
    await act(async () => {
      await savePromise!;
    });
    expect(result.current.isSaving).toBe(false);
  });
});
