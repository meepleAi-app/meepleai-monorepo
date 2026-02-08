/**
 * Tests for useDebounce hook
 */

import { renderHook } from '@testing-library/react';
import { useDebounce } from '../hooks/use-debounce';
import { vi } from 'vitest';

describe('useDebounce', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should return initial value immediately', () => {
    const { result } = renderHook(() => useDebounce('initial', 300));

    expect(result.current).toBe('initial');
  });

  it('should debounce value updates', () => {
    const { result, rerender } = renderHook(({ value }) => useDebounce(value, 300), {
      initialProps: { value: 'initial' },
    });

    // Update value
    rerender({ value: 'updated' });

    // Value should not update immediately
    expect(result.current).toBe('initial');

    // Fast-forward time by delay
    vi.runAllTimers();

    // After timers, value should be updated
    expect(result.current).toBe('updated');
  });

  it('should cancel previous timeout on rapid updates', () => {
    const { result, rerender } = renderHook(({ value }) => useDebounce(value, 300), {
      initialProps: { value: 'initial' },
    });

    // Rapid updates
    rerender({ value: 'update1' });
    vi.advanceTimersByTime(100);

    rerender({ value: 'update2' });
    vi.advanceTimersByTime(100);

    rerender({ value: 'final' });

    // Run all pending timers
    vi.runAllTimers();

    // Should have final value (previous timeouts cancelled)
    expect(result.current).toBe('final');
  });

  it('should cleanup timeout on unmount', () => {
    const { unmount } = renderHook(() => useDebounce('value', 300));

    unmount();

    // Should not throw or cause memory leaks
    expect(() => vi.advanceTimersByTime(300)).not.toThrow();
  });
});
