/**
 * useCounterTool Hook Tests (Issue #4979)
 *
 * Coverage:
 * - Initial state: state null, isPending false, error null
 * - applyChange shared: optimistic update, API call, server reconciliation
 * - applyChange per-player: updates correct player value
 * - Clamp: values stay within [minValue, maxValue]
 * - Error revert: on API failure, state reverts to previous
 * - applyState: sets state directly (for SSE)
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, afterEach } from 'vitest';

import { useCounterTool } from '../useCounterTool';
import type { CounterState } from '@/components/session/types';

// ── Fixtures ──────────────────────────────────────────────────────────────────

const SHARED_STATE: CounterState = {
  minValue: 0,
  maxValue: 100,
  defaultValue: 50,
  isPerPlayer: false,
  currentValue: 50,
  playerValues: {},
};

const PER_PLAYER_STATE: CounterState = {
  ...SHARED_STATE,
  isPerPlayer: true,
  playerValues: { 'p1': 30, 'p2': 45 },
};

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useCounterTool', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ── Initial state ────────────────────────────────────────────────────────────

  describe('initial state', () => {
    it('starts with state=null, isPending=false, error=null', () => {
      const { result } = renderHook(() =>
        useCounterTool({
          sessionId: 'sess-1',
          toolName: 'points',
          
          apiBaseUrl: '',
        })
      );

      expect(result.current.state).toBeNull();
      expect(result.current.isPending).toBe(false);
      expect(result.current.error).toBeNull();
    });
  });

  // ── applyState (SSE integration) ─────────────────────────────────────────────

  describe('applyState', () => {
    it('sets state directly without API call', () => {
      const { result } = renderHook(() =>
        useCounterTool({
          sessionId: 'sess-1',
          toolName: 'points',
          
          apiBaseUrl: '',
        })
      );

      act(() => {
        result.current.applyState(SHARED_STATE);
      });

      expect(result.current.state).toEqual(SHARED_STATE);
    });
  });

  // ── applyChange: shared mode ──────────────────────────────────────────────────

  describe('applyChange (shared mode)', () => {
    it('applies optimistic update before API response', async () => {
      let resolveApi!: (val: unknown) => void;
      global.fetch = vi.fn().mockReturnValue(
        new Promise(res => { resolveApi = res; })
      );

      const { result } = renderHook(() =>
        useCounterTool({ sessionId: 's', toolName: 't', currentUserId: 'p1', apiBaseUrl: '' })
      );

      // Seed initial state
      act(() => result.current.applyState(SHARED_STATE));

      // Start change without awaiting
      act(() => { void result.current.applyChange('p1', 5); });

      // Optimistic: currentValue should be 55 before API resolves
      expect(result.current.state?.currentValue).toBe(55);

      // Resolve the API
      resolveApi({ ok: true, status: 200, json: vi.fn().mockResolvedValue({ stateData: { ...SHARED_STATE, currentValue: 55 } }) });
      await waitFor(() => expect(result.current.isPending).toBe(false));
    });

    it('reconciles with server response', async () => {
      const serverState = { ...SHARED_STATE, currentValue: 55 };
      global.fetch = vi.fn().mockResolvedValue({
        ok: true, status: 200,
        json: vi.fn().mockResolvedValue({ stateData: serverState }),
      });

      const { result } = renderHook(() =>
        useCounterTool({ sessionId: 's', toolName: 't', currentUserId: 'p1', apiBaseUrl: '' })
      );

      act(() => result.current.applyState(SHARED_STATE));

      await act(async () => { await result.current.applyChange('p1', 5); });

      expect(result.current.state).toEqual(serverState);
    });

    it('reverts optimistic update on API failure', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false, status: 500, statusText: 'Internal Server Error',
        json: vi.fn(),
      });

      const { result } = renderHook(() =>
        useCounterTool({ sessionId: 's', toolName: 't', currentUserId: 'p1', apiBaseUrl: '' })
      );

      act(() => result.current.applyState(SHARED_STATE));

      await act(async () => { await result.current.applyChange('p1', 5); });

      // Should revert to 50
      expect(result.current.state?.currentValue).toBe(50);
      expect(result.current.error).toMatch(/HTTP 500/);
    });

    it('clamps value at maxValue', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true, status: 200,
        json: vi.fn().mockResolvedValue({ stateData: { ...SHARED_STATE, currentValue: 100 } }),
      });

      const { result } = renderHook(() =>
        useCounterTool({ sessionId: 's', toolName: 't', currentUserId: 'p1', apiBaseUrl: '' })
      );

      act(() => result.current.applyState({ ...SHARED_STATE, currentValue: 98 }));
      act(() => { void result.current.applyChange('p1', 10); });

      // Clamped optimistically at 100
      expect(result.current.state?.currentValue).toBe(100);
    });

    it('clamps value at minValue', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true, status: 200,
        json: vi.fn().mockResolvedValue({ stateData: { ...SHARED_STATE, currentValue: 0 } }),
      });

      const { result } = renderHook(() =>
        useCounterTool({ sessionId: 's', toolName: 't', currentUserId: 'p1', apiBaseUrl: '' })
      );

      act(() => result.current.applyState({ ...SHARED_STATE, currentValue: 2 }));
      act(() => { void result.current.applyChange('p1', -10); });

      // Clamped at 0
      expect(result.current.state?.currentValue).toBe(0);
    });

    it('does nothing when state is null', async () => {
      global.fetch = vi.fn();

      const { result } = renderHook(() =>
        useCounterTool({ sessionId: 's', toolName: 't', currentUserId: 'p1', apiBaseUrl: '' })
      );

      await act(async () => { await result.current.applyChange('p1', 5); });

      expect(global.fetch).not.toHaveBeenCalled();
    });
  });

  // ── applyChange: per-player mode ──────────────────────────────────────────────

  describe('applyChange (per-player mode)', () => {
    it('updates only the specified player value', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true, status: 200,
        json: vi.fn().mockResolvedValue({
          stateData: {
            ...PER_PLAYER_STATE,
            playerValues: { p1: 35, p2: 45 },
          },
        }),
      });

      const { result } = renderHook(() =>
        useCounterTool({ sessionId: 's', toolName: 't', currentUserId: 'p1', apiBaseUrl: '' })
      );

      act(() => result.current.applyState(PER_PLAYER_STATE));

      act(() => { void result.current.applyChange('p1', 5); });

      // Optimistic: p1 goes from 30 to 35, p2 stays at 45
      expect(result.current.state?.playerValues?.['p1']).toBe(35);
      expect(result.current.state?.playerValues?.['p2']).toBe(45);
    });

    it('initializes unknown player to defaultValue before applying change', async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: true, status: 200,
        json: vi.fn().mockResolvedValue({ stateData: PER_PLAYER_STATE }),
      });

      const { result } = renderHook(() =>
        useCounterTool({ sessionId: 's', toolName: 't', currentUserId: 'p1', apiBaseUrl: '' })
      );

      const stateNoP3: CounterState = { ...PER_PLAYER_STATE, playerValues: { p1: 30, p2: 45 } };
      act(() => result.current.applyState(stateNoP3));

      act(() => { void result.current.applyChange('p3', 10); });

      // p3 was unknown → starts at defaultValue (50), then +10 = 60
      expect(result.current.state?.playerValues?.['p3']).toBe(60);
    });
  });

  // ── isPending ────────────────────────────────────────────────────────────────

  describe('isPending', () => {
    it('is true while API call is in flight', async () => {
      let resolveApi!: (val: unknown) => void;
      global.fetch = vi.fn().mockReturnValue(
        new Promise(res => { resolveApi = res; })
      );

      const { result } = renderHook(() =>
        useCounterTool({ sessionId: 's', toolName: 't', currentUserId: 'p1', apiBaseUrl: '' })
      );

      act(() => result.current.applyState(SHARED_STATE));
      act(() => { void result.current.applyChange('p1', 1); });

      expect(result.current.isPending).toBe(true);

      resolveApi({ ok: true, status: 200, json: vi.fn().mockResolvedValue({ stateData: SHARED_STATE }) });
      await waitFor(() => expect(result.current.isPending).toBe(false));
    });
  });
});
