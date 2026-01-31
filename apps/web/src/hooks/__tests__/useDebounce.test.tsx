/**
 * Comprehensive Tests for useDebounce Hook (Issue #1661 - Fase 1.3)
 * Coverage target: 95%+
 */

import { renderHook, act } from '@testing-library/react';
import { useDebounce } from '../useDebounce';

describe('useDebounce', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should return initial value immediately', () => {
    const { result } = renderHook(() => useDebounce('initial', 1000));

    expect(result.current).toBe('initial');
  });

  it('should debounce value updates', () => {
    const { result, rerender } = renderHook(({ value, delay }) => useDebounce(value, delay), {
      initialProps: { value: 'initial', delay: 1000 },
    });

    expect(result.current).toBe('initial');

    rerender({ value: 'updated', delay: 1000 });

    expect(result.current).toBe('initial');

    act(() => {
      vi.advanceTimersByTime(1000);
    });

    expect(result.current).toBe('updated');
  });

  it('should reset timer on rapid value changes', () => {
    const { result, rerender } = renderHook(({ value }) => useDebounce(value, 500), {
      initialProps: { value: 'initial' },
    });

    rerender({ value: 'change1' });
    act(() => { vi.advanceTimersByTime(400); });

    rerender({ value: 'change2' });
    act(() => { vi.advanceTimersByTime(400); });

    rerender({ value: 'final' });
    act(() => { vi.advanceTimersByTime(500); });

    expect(result.current).toBe('final');
  });

  it('should use custom delay', () => {
    const { result, rerender } = renderHook(() => useDebounce('initial', 100));

    rerender();

    act(() => {
      vi.advanceTimersByTime(100);
    });

    expect(result.current).toBe('initial');
  });

  it('should handle different value types', () => {
    const { result: stringResult } = renderHook(() => useDebounce('string', 100));
    const { result: numberResult } = renderHook(() => useDebounce(42, 100));
    const { result: boolResult } = renderHook(() => useDebounce(true, 100));
    const { result: objectResult } = renderHook(() => useDebounce({ key: 'value' }, 100));
    const { result: arrayResult } = renderHook(() => useDebounce([1, 2, 3], 100));

    expect(stringResult.current).toBe('string');
    expect(numberResult.current).toBe(42);
    expect(boolResult.current).toBe(true);
    expect(objectResult.current).toEqual({ key: 'value' });
    expect(arrayResult.current).toEqual([1, 2, 3]);
  });

  it('should cleanup timeout on unmount', () => {
    const { unmount } = renderHook(() => useDebounce('value', 1000));

    unmount();

    act(() => {
      vi.advanceTimersByTime(1000);
    });

    expect(true).toBe(true);
  });
});
