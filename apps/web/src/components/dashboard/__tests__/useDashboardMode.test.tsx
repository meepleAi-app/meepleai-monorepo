/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import React from 'react';

import { useDashboardMode } from '../useDashboardMode';
import { DashboardEngineProvider } from '../DashboardEngineProvider';

// ---------------------------------------------------------------------------
// Mock sessionStore — vi.hoisted so factory can reference the mock fn
// ---------------------------------------------------------------------------
const { mockGetState } = vi.hoisted(() => ({
  mockGetState: vi.fn().mockReturnValue({ activeSession: null }),
}));

vi.mock('@/lib/stores/sessionStore', () => ({
  useSessionStore: Object.assign(
    (selector?: (state: unknown) => unknown) => {
      const state = mockGetState();
      return selector ? selector(state) : state;
    },
    {
      getState: mockGetState,
      setState: vi.fn(),
      subscribe: vi.fn().mockReturnValue(() => {}),
      destroy: vi.fn(),
    }
  ),
}));

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function wrapper({ children }: { children: React.ReactNode }) {
  return <DashboardEngineProvider>{children}</DashboardEngineProvider>;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('useDashboardMode', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetState.mockReturnValue({ activeSession: null });
  });

  describe('without provider (fallback)', () => {
    it('returns exploration defaults when used outside DashboardEngineProvider', () => {
      const { result } = renderHook(() => useDashboardMode());

      expect(result.current.state).toBe('exploration');
      expect(result.current.isExploration).toBe(true);
      expect(result.current.isGameMode).toBe(false);
      expect(result.current.isTransitioning).toBe(false);
      expect(result.current.isExpanded).toBe(false);
      expect(result.current.activeSessionId).toBeNull();
      expect(result.current.transitionTarget).toBeNull();
      expect(typeof result.current.send).toBe('function');
    });

    it('send is a noop outside provider', () => {
      const { result } = renderHook(() => useDashboardMode());

      // Should not throw
      expect(() => {
        result.current.send({ type: 'SESSION_DETECTED', sessionId: 'x' });
      }).not.toThrow();
    });
  });

  describe('with provider (initial state)', () => {
    it('returns exploration as initial state', () => {
      const { result } = renderHook(() => useDashboardMode(), { wrapper });

      expect(result.current.state).toBe('exploration');
      expect(result.current.isExploration).toBe(true);
      expect(result.current.isGameMode).toBe(false);
      expect(result.current.isTransitioning).toBe(false);
      expect(result.current.activeSessionId).toBeNull();
    });
  });

  describe('state transitions via send', () => {
    it('transitions to transitioning on SESSION_DETECTED', () => {
      const { result } = renderHook(() => useDashboardMode(), { wrapper });

      act(() => {
        result.current.send({ type: 'SESSION_DETECTED', sessionId: 'sess-42' });
      });

      expect(result.current.state).toBe('transitioning');
      expect(result.current.isTransitioning).toBe(true);
      expect(result.current.isExploration).toBe(false);
      expect(result.current.activeSessionId).toBe('sess-42');
      expect(result.current.transitionTarget).toBe('gameMode');
    });

    it('transitions to gameMode after TRANSITION_COMPLETE', () => {
      const { result } = renderHook(() => useDashboardMode(), { wrapper });

      act(() => {
        result.current.send({ type: 'SESSION_DETECTED', sessionId: 'sess-42' });
      });
      act(() => {
        result.current.send({ type: 'TRANSITION_COMPLETE' });
      });

      expect(result.current.state).toBe('gameMode');
      expect(result.current.isGameMode).toBe(true);
      expect(result.current.isTransitioning).toBe(false);
      expect(result.current.activeSessionId).toBe('sess-42');
    });

    it('tracks expanded sub-state', () => {
      const { result } = renderHook(() => useDashboardMode(), { wrapper });

      act(() => {
        result.current.send({ type: 'SESSION_DETECTED', sessionId: 'sess-42' });
      });
      act(() => {
        result.current.send({ type: 'TRANSITION_COMPLETE' });
      });

      expect(result.current.isExpanded).toBe(false);

      act(() => {
        result.current.send({ type: 'EXPAND' });
      });

      expect(result.current.isExpanded).toBe(true);

      act(() => {
        result.current.send({ type: 'COLLAPSE' });
      });

      expect(result.current.isExpanded).toBe(false);
    });

    it('returns to exploration after SESSION_COMPLETED + TRANSITION_COMPLETE', () => {
      const { result } = renderHook(() => useDashboardMode(), { wrapper });

      // Go to game mode
      act(() => {
        result.current.send({ type: 'SESSION_DETECTED', sessionId: 'sess-42' });
      });
      act(() => {
        result.current.send({ type: 'TRANSITION_COMPLETE' });
      });
      expect(result.current.isGameMode).toBe(true);

      // End session
      act(() => {
        result.current.send({ type: 'SESSION_COMPLETED' });
      });
      expect(result.current.isTransitioning).toBe(true);
      expect(result.current.transitionTarget).toBe('exploration');

      // Complete exit transition
      act(() => {
        result.current.send({ type: 'TRANSITION_COMPLETE' });
      });
      expect(result.current.isExploration).toBe(true);
      expect(result.current.activeSessionId).toBeNull();
    });
  });

  describe('activeSessionId from context', () => {
    it('exposes activeSessionId from machine context', () => {
      const { result } = renderHook(() => useDashboardMode(), { wrapper });

      expect(result.current.activeSessionId).toBeNull();

      act(() => {
        result.current.send({ type: 'SESSION_DETECTED', sessionId: 'my-session' });
      });

      expect(result.current.activeSessionId).toBe('my-session');
    });
  });
});
