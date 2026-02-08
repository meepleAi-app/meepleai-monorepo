/**
 * Tests for useDebounce hook
 */

import { renderHook, waitFor } from '@testing-library/react';
import { useDebounce } from '../hooks/use-debounce';

describe('useDebounce', () => {
  it('should return initial value immediately', () => {
    const { result } = renderHook(() => useDebounce('initial', 50));

    expect(result.current).toBe('initial');
  });

  it('should debounce value updates', async () => {
    const { result, rerender } = renderHook(({ value }) => useDebounce(value, 50), {
      initialProps: { value: 'initial' },
    });

    // Update value
    rerender({ value: 'updated' });

    // Value should not update immediately
    expect(result.current).toBe('initial');

    // Wait for debounce delay
    await waitFor(
      () => {
        expect(result.current).toBe('updated');
      },
      { timeout: 200 }
    );
  });

  it('should cancel previous timeout on rapid updates', async () => {
    const { result, rerender } = renderHook(({ value }) => useDebounce(value, 50), {
      initialProps: { value: 'initial' },
    });

    // Rapid updates (should only apply last one)
    rerender({ value: 'update1' });
    await new Promise((resolve) => setTimeout(resolve, 20));

    rerender({ value: 'update2' });
    await new Promise((resolve) => setTimeout(resolve, 20));

    rerender({ value: 'final' });

    // Wait for final debounce
    await waitFor(
      () => {
        expect(result.current).toBe('final');
      },
      { timeout: 200 }
    );
  });

  it('should cleanup timeout on unmount', async () => {
    const { unmount } = renderHook(() => useDebounce('value', 50));

    unmount();

    // Should not cause errors after unmount
    await new Promise((resolve) => setTimeout(resolve, 100));
  });
});
