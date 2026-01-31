/**
 * useMediaQuery Hook Tests - Issue #3026
 *
 * Test coverage:
 * - Initial state
 * - Media query matching
 * - Event listener updates
 * - Cleanup on unmount
 * - Query change handling
 *
 * Target: >90% coverage
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';

import { useMediaQuery } from '../useMediaQuery';

// Mock matchMedia
const createMatchMediaMock = (matches: boolean) => {
  const listeners: Array<(event: MediaQueryListEvent) => void> = [];

  return {
    matches,
    media: '',
    addEventListener: vi.fn((event: string, listener: (event: MediaQueryListEvent) => void) => {
      if (event === 'change') {
        listeners.push(listener);
      }
    }),
    removeEventListener: vi.fn((event: string, listener: (event: MediaQueryListEvent) => void) => {
      if (event === 'change') {
        const index = listeners.indexOf(listener);
        if (index > -1) {
          listeners.splice(index, 1);
        }
      }
    }),
    dispatchEvent: vi.fn(),
    onchange: null,
    addListener: vi.fn(), // Deprecated
    removeListener: vi.fn(), // Deprecated
    // Helper to trigger change events
    triggerChange: (newMatches: boolean) => {
      listeners.forEach(listener => {
        listener({ matches: newMatches } as MediaQueryListEvent);
      });
    },
  };
};

describe('useMediaQuery', () => {
  let matchMediaMock: ReturnType<typeof createMatchMediaMock>;

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Initial State', () => {
    it('should return false for non-matching query', () => {
      matchMediaMock = createMatchMediaMock(false);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() => useMediaQuery('(min-width: 768px)'));

      expect(result.current).toBe(false);
    });

    it('should return true for matching query', () => {
      matchMediaMock = createMatchMediaMock(true);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() => useMediaQuery('(min-width: 768px)'));

      expect(result.current).toBe(true);
    });

    it('should call matchMedia with correct query', () => {
      matchMediaMock = createMatchMediaMock(false);
      const matchMediaSpy = vi.fn(() => matchMediaMock);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: matchMediaSpy,
      });

      renderHook(() => useMediaQuery('(min-width: 1024px)'));

      expect(matchMediaSpy).toHaveBeenCalledWith('(min-width: 1024px)');
    });
  });

  describe('Event Listener', () => {
    it('should add event listener on mount', () => {
      matchMediaMock = createMatchMediaMock(false);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      renderHook(() => useMediaQuery('(min-width: 768px)'));

      expect(matchMediaMock.addEventListener).toHaveBeenCalledWith('change', expect.any(Function));
    });

    it('should update when media query matches', async () => {
      matchMediaMock = createMatchMediaMock(false);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() => useMediaQuery('(min-width: 768px)'));

      expect(result.current).toBe(false);

      // Trigger media query change
      matchMediaMock.triggerChange(true);

      await waitFor(() => {
        expect(result.current).toBe(true);
      });
    });

    it('should update when media query stops matching', async () => {
      matchMediaMock = createMatchMediaMock(true);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() => useMediaQuery('(min-width: 768px)'));

      expect(result.current).toBe(true);

      // Trigger media query change
      matchMediaMock.triggerChange(false);

      await waitFor(() => {
        expect(result.current).toBe(false);
      });
    });

    it('should handle multiple changes', async () => {
      matchMediaMock = createMatchMediaMock(false);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() => useMediaQuery('(min-width: 768px)'));

      expect(result.current).toBe(false);

      matchMediaMock.triggerChange(true);
      await waitFor(() => expect(result.current).toBe(true));

      matchMediaMock.triggerChange(false);
      await waitFor(() => expect(result.current).toBe(false));

      matchMediaMock.triggerChange(true);
      await waitFor(() => expect(result.current).toBe(true));
    });
  });

  describe('Cleanup', () => {
    it('should remove event listener on unmount', () => {
      matchMediaMock = createMatchMediaMock(false);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { unmount } = renderHook(() => useMediaQuery('(min-width: 768px)'));

      unmount();

      expect(matchMediaMock.removeEventListener).toHaveBeenCalledWith('change', expect.any(Function));
    });

    it('should not update after unmount', async () => {
      matchMediaMock = createMatchMediaMock(false);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result, unmount } = renderHook(() => useMediaQuery('(min-width: 768px)'));

      expect(result.current).toBe(false);

      unmount();

      // Trigger change after unmount (should not cause errors)
      matchMediaMock.triggerChange(true);

      // Wait a bit to ensure no state updates
      await new Promise(resolve => setTimeout(resolve, 50));

      // State should remain false (from before unmount)
      expect(result.current).toBe(false);
    });
  });

  describe('Query Changes', () => {
    it('should update listener when query changes', () => {
      const mockQuery1 = createMatchMediaMock(false);
      const mockQuery2 = createMatchMediaMock(true);

      const matchMediaSpy = vi
        .fn()
        .mockReturnValueOnce(mockQuery1)
        .mockReturnValueOnce(mockQuery2);

      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: matchMediaSpy,
      });

      const { result, rerender } = renderHook(
        ({ query }) => useMediaQuery(query),
        { initialProps: { query: '(min-width: 768px)' } }
      );

      expect(result.current).toBe(false);

      // Change query
      rerender({ query: '(min-width: 1024px)' });

      expect(matchMediaSpy).toHaveBeenCalledWith('(min-width: 1024px)');
      expect(result.current).toBe(true);
    });

    it('should clean up old listener when query changes', () => {
      const mockQuery1 = createMatchMediaMock(false);
      const mockQuery2 = createMatchMediaMock(true);

      const matchMediaSpy = vi
        .fn()
        .mockReturnValueOnce(mockQuery1)
        .mockReturnValueOnce(mockQuery2);

      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: matchMediaSpy,
      });

      const { rerender } = renderHook(
        ({ query }) => useMediaQuery(query),
        { initialProps: { query: '(min-width: 768px)' } }
      );

      rerender({ query: '(min-width: 1024px)' });

      expect(mockQuery1.removeEventListener).toHaveBeenCalledWith('change', expect.any(Function));
      expect(mockQuery2.addEventListener).toHaveBeenCalledWith('change', expect.any(Function));
    });
  });

  describe('Edge Cases', () => {
    it('should handle prefers-color-scheme query', () => {
      matchMediaMock = createMatchMediaMock(true);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() => useMediaQuery('(prefers-color-scheme: dark)'));

      expect(result.current).toBe(true);
    });

    it('should handle orientation query', () => {
      matchMediaMock = createMatchMediaMock(false);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() => useMediaQuery('(orientation: landscape)'));

      expect(result.current).toBe(false);
    });

    it('should handle complex media queries', () => {
      matchMediaMock = createMatchMediaMock(true);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() =>
        useMediaQuery('(min-width: 768px) and (max-width: 1024px)')
      );

      expect(result.current).toBe(true);
    });

    it('should handle max-width query', () => {
      matchMediaMock = createMatchMediaMock(true);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() => useMediaQuery('(max-width: 640px)'));

      expect(result.current).toBe(true);
    });
  });

  describe('Responsive Breakpoints', () => {
    it('should handle mobile breakpoint (< 768px)', async () => {
      matchMediaMock = createMatchMediaMock(false);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() => useMediaQuery('(min-width: 768px)'));

      expect(result.current).toBe(false); // Mobile

      matchMediaMock.triggerChange(true);
      await waitFor(() => expect(result.current).toBe(true)); // Desktop
    });

    it('should handle tablet breakpoint (768px - 1024px)', async () => {
      matchMediaMock = createMatchMediaMock(true);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() =>
        useMediaQuery('(min-width: 768px) and (max-width: 1024px)')
      );

      expect(result.current).toBe(true);
    });

    it('should handle desktop breakpoint (> 1024px)', () => {
      matchMediaMock = createMatchMediaMock(true);
      Object.defineProperty(window, 'matchMedia', {
        writable: true,
        value: vi.fn(() => matchMediaMock),
      });

      const { result } = renderHook(() => useMediaQuery('(min-width: 1024px)'));

      expect(result.current).toBe(true);
    });
  });
});
