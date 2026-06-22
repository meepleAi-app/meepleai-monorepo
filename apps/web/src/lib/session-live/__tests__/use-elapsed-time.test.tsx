import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useElapsedTime } from '../use-elapsed-time';

describe('useElapsedTime', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-06-15T10:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('returns undefined when startedAt is undefined', () => {
    const { result } = renderHook(() => useElapsedTime(undefined));
    expect(result.current).toBeUndefined();
  });

  it('returns undefined when startedAt is null', () => {
    const { result } = renderHook(() => useElapsedTime(null));
    expect(result.current).toBeUndefined();
  });

  it('returns undefined for invalid ISO string (defensive)', () => {
    const { result } = renderHook(() => useElapsedTime('not-a-date'));
    expect(result.current).toBeUndefined();
  });

  it('returns 0 when startedAt equals current time', () => {
    const { result } = renderHook(() => useElapsedTime('2026-06-15T10:00:00Z'));
    expect(result.current).toBe(0);
  });

  it('returns elapsed ms when startedAt is in the past', () => {
    const { result } = renderHook(() => useElapsedTime('2026-06-15T09:30:00Z'));
    // 30 minutes = 1_800_000 ms
    expect(result.current).toBe(1_800_000);
  });

  it('ticks every second by default', () => {
    const { result } = renderHook(() => useElapsedTime('2026-06-15T09:59:55Z'));
    expect(result.current).toBe(5_000); // 5 seconds elapsed initially

    act(() => {
      vi.advanceTimersByTime(1_000);
    });
    expect(result.current).toBe(6_000);

    act(() => {
      vi.advanceTimersByTime(2_000);
    });
    expect(result.current).toBe(8_000);
  });

  it('returns 0 (not negative) for startedAt in the future', () => {
    const { result } = renderHook(() => useElapsedTime('2026-06-15T11:00:00Z'));
    expect(result.current).toBe(0);
  });

  it('respects custom intervalMs', () => {
    const { result } = renderHook(() => useElapsedTime('2026-06-15T09:59:55Z', 500));
    expect(result.current).toBe(5_000);

    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(result.current).toBe(5_500);
  });

  it('clears interval on unmount (no leaked timers)', () => {
    const { unmount } = renderHook(() => useElapsedTime('2026-06-15T10:00:00Z'));
    unmount();
    // If interval leaked, advanceTimersByTime would call setTick on unmounted component
    // (React would log a warning). Test passes if no warning.
    act(() => {
      vi.advanceTimersByTime(5_000);
    });
  });
});
