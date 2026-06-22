/**
 * useDebouncedCallback unit tests — Issue #2430 Block B+ (T4).
 *
 * 5 cases: delayed fire, only-last-call, flush invokes pending,
 * flush no-op, unmount clears timer without auto-flush.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import { useDebouncedCallback } from '@/lib/session-live/use-debounced-callback';

describe('useDebouncedCallback', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('fires the callback after delay when called once', () => {
    const cb = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(cb, 500));

    act(() => {
      const [debouncedFn] = result.current;
      debouncedFn('a');
    });
    expect(cb).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(cb).toHaveBeenCalledTimes(1);
    expect(cb).toHaveBeenCalledWith('a');
  });

  it('only fires the last call when invoked multiple times within the window', () => {
    const cb = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(cb, 500));

    act(() => {
      const [debouncedFn] = result.current;
      debouncedFn('first');
      vi.advanceTimersByTime(100);
      debouncedFn('second');
      vi.advanceTimersByTime(100);
      debouncedFn('third');
    });

    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(cb).toHaveBeenCalledTimes(1);
    expect(cb).toHaveBeenCalledWith('third');
  });

  it('flush() invokes the pending callback immediately', () => {
    const cb = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(cb, 500));

    act(() => {
      const [debouncedFn] = result.current;
      debouncedFn('pending');
    });
    expect(cb).not.toHaveBeenCalled();

    act(() => {
      const [, flush] = result.current;
      flush();
    });
    expect(cb).toHaveBeenCalledTimes(1);
    expect(cb).toHaveBeenCalledWith('pending');
  });

  it('flush() is a no-op when nothing is pending', () => {
    const cb = vi.fn();
    const { result } = renderHook(() => useDebouncedCallback(cb, 500));

    act(() => {
      const [, flush] = result.current;
      flush();
    });
    expect(cb).not.toHaveBeenCalled();
  });

  it('unmount clears the timer without auto-flushing', () => {
    const cb = vi.fn();
    const { result, unmount } = renderHook(() => useDebouncedCallback(cb, 500));

    act(() => {
      const [debouncedFn] = result.current;
      debouncedFn('about-to-be-orphaned');
    });
    expect(cb).not.toHaveBeenCalled();

    unmount();

    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(cb).not.toHaveBeenCalled();
  });
});
