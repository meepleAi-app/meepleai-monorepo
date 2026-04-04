import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

import { useViewTransition } from '../useViewTransition';

describe('useViewTransition', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    mockPush.mockClear();
  });

  it('falls back to router.push when startViewTransition not available', () => {
    // @ts-expect-error - testing absence
    document.startViewTransition = undefined;
    const { result } = renderHook(() => useViewTransition());
    result.current.navigateWithTransition('/dashboard');
    expect(mockPush).toHaveBeenCalledWith('/dashboard');
  });

  it('wraps navigation in startViewTransition when available', () => {
    const mockTransition = vi.fn((cb: () => void) => {
      cb();
      return {
        finished: Promise.resolve(),
        ready: Promise.resolve(),
        updateCallbackDone: Promise.resolve(),
      };
    });
    (document as any).startViewTransition = mockTransition;
    const { result } = renderHook(() => useViewTransition());
    result.current.navigateWithTransition('/games');
    expect(mockTransition).toHaveBeenCalledOnce();
    expect(mockPush).toHaveBeenCalledWith('/games');
    delete (document as any).startViewTransition;
  });

  it('does not call startViewTransition when it is not a function', () => {
    // Ensure non-function values are treated as unavailable
    (document as any).startViewTransition = null;
    const { result } = renderHook(() => useViewTransition());
    result.current.navigateWithTransition('/settings');
    expect(mockPush).toHaveBeenCalledWith('/settings');
    delete (document as any).startViewTransition;
  });
});
