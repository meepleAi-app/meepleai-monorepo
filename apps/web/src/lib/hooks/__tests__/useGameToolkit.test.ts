/**
 * useGameToolkit Hook Tests (Issue #4976)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';

import { useGameToolkit } from '../useGameToolkit';
import type { GameToolkitDto } from '../../types/gameToolkit';

// ── Helpers ───────────────────────────────────────────────────────────────────

const mockToolkit: GameToolkitDto = {
  id: 'toolkit-1',
  gameId: 'game-abc',
  privateGameId: null,
  name: 'Catan Toolkit',
  version: 2,
  isPublished: true,
  overridesTurnOrder: false,
  overridesScoreboard: false,
  overridesDiceSet: true,
  diceTools: [{ name: '2×d6', diceType: 'd6', quantity: 2, isInteractive: true }],
  cardTools: [],
  timerTools: [],
  counterTools: [],
};

// ── Setup ─────────────────────────────────────────────────────────────────────

beforeEach(() => {
  global.fetch = vi.fn();
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useGameToolkit', () => {
  it('returns null toolkit and no loading when gameId is null', () => {
    const { result } = renderHook(() => useGameToolkit(null));
    expect(result.current.toolkit).toBeNull();
    expect(result.current.isLoading).toBe(false);
    expect(result.current.error).toBeNull();
    expect(global.fetch).not.toHaveBeenCalled();
  });

  it('returns null toolkit and no loading when gameId is undefined', () => {
    const { result } = renderHook(() => useGameToolkit(undefined));
    expect(result.current.toolkit).toBeNull();
    expect(result.current.isLoading).toBe(false);
    expect(result.current.error).toBeNull();
    expect(global.fetch).not.toHaveBeenCalled();
  });

  it('fetches toolkit for a given gameId', async () => {
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => [mockToolkit],
    });

    const { result } = renderHook(() => useGameToolkit('game-abc'));

    expect(result.current.isLoading).toBe(true);

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit).toEqual(mockToolkit);
    expect(result.current.error).toBeNull();
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/game-toolkits/by-game/game-abc'),
      expect.objectContaining({ method: 'GET', credentials: 'include' }),
    );
  });

  it('returns the first published toolkit when multiple exist', async () => {
    const unpublished: GameToolkitDto = { ...mockToolkit, id: 'toolkit-old', isPublished: false };
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => [unpublished, mockToolkit],
    });

    const { result } = renderHook(() => useGameToolkit('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit?.id).toBe('toolkit-1');
  });

  it('returns null toolkit when no published toolkit exists', async () => {
    const unpublished: GameToolkitDto = { ...mockToolkit, isPublished: false };
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => [unpublished],
    });

    const { result } = renderHook(() => useGameToolkit('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit).toBeNull();
    expect(result.current.error).toBeNull();
  });

  it('returns null toolkit on 404 (no toolkit configured for game)', async () => {
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 404,
      statusText: 'Not Found',
    });

    const { result } = renderHook(() => useGameToolkit('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit).toBeNull();
    expect(result.current.error).toBeNull();
  });

  it('sets error on HTTP failure (non-404)', async () => {
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 500,
      statusText: 'Internal Server Error',
    });

    const { result } = renderHook(() => useGameToolkit('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit).toBeNull();
    expect(result.current.error).toContain('HTTP 500');
  });

  it('sets error on network failure', async () => {
    (global.fetch as ReturnType<typeof vi.fn>).mockRejectedValueOnce(
      new Error('Network error'),
    );

    const { result } = renderHook(() => useGameToolkit('game-abc'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.toolkit).toBeNull();
    expect(result.current.error).toBe('Network error');
  });

  it('re-fetches when gameId changes', async () => {
    const toolkit2: GameToolkitDto = { ...mockToolkit, id: 'toolkit-2', gameId: 'game-xyz' };

    (global.fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({ ok: true, json: async () => [mockToolkit] })
      .mockResolvedValueOnce({ ok: true, json: async () => [toolkit2] });

    const { result, rerender } = renderHook(
      ({ gameId }: { gameId: string }) => useGameToolkit(gameId),
      { initialProps: { gameId: 'game-abc' } },
    );

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.toolkit?.id).toBe('toolkit-1');

    rerender({ gameId: 'game-xyz' });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.toolkit?.id).toBe('toolkit-2');
    expect(global.fetch).toHaveBeenCalledTimes(2);
  });

  it('resets to null when gameId becomes null', async () => {
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => [mockToolkit],
    });

    const { result, rerender } = renderHook(
      ({ gameId }: { gameId: string | null }) => useGameToolkit(gameId),
      { initialProps: { gameId: 'game-abc' as string | null } },
    );

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.toolkit).not.toBeNull();

    act(() => {
      rerender({ gameId: null });
    });

    expect(result.current.toolkit).toBeNull();
  });
});
