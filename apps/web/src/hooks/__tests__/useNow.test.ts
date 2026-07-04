/**
 * @vitest-environment jsdom
 *
 * useNow unit tests — #2633 Slice B (LD-7).
 *
 * The night-live header/elapsed derives from an injected `now`; this hook is the
 * ticking clock that keeps it live without polling the server. Keeping the tick
 * OUTSIDE the pure mapper is what makes the mapper deterministic.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useNow } from '../useNow';

describe('useNow', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-07-04T20:00:00Z'));
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('returns a Date on first render', () => {
    const { result } = renderHook(() => useNow(60_000));
    expect(result.current).toBeInstanceOf(Date);
    expect(result.current.toISOString()).toBe('2026-07-04T20:00:00.000Z');
  });

  it('advances the returned time after the interval elapses', () => {
    const { result } = renderHook(() => useNow(60_000));
    const first = result.current.getTime();
    act(() => {
      vi.advanceTimersByTime(60_000);
    });
    expect(result.current.getTime()).toBeGreaterThan(first);
  });

  it('does not tick before the interval elapses', () => {
    const { result } = renderHook(() => useNow(60_000));
    const first = result.current.getTime();
    act(() => {
      vi.advanceTimersByTime(59_000);
    });
    expect(result.current.getTime()).toBe(first);
  });

  it('clears the interval on unmount', () => {
    const clearSpy = vi.spyOn(globalThis, 'clearInterval');
    const { unmount } = renderHook(() => useNow(60_000));
    unmount();
    expect(clearSpy).toHaveBeenCalled();
  });
});
