/**
 * Tests for useReducedMotion hook
 * Verifies media query detection and SSR safety
 */

import { renderHook } from '@testing-library/react';
import { useReducedMotion } from '../useReducedMotion';

describe('useReducedMotion', () => {
  let matchMediaMock: jest.Mock;
  let addEventListenerSpy: jest.Mock;
  let removeEventListenerSpy: jest.Mock;
  let addListenerSpy: jest.Mock;
  let removeListenerSpy: jest.Mock;

  beforeEach(() => {
    // Reset spies
    addEventListenerSpy = jest.fn();
    removeEventListenerSpy = jest.fn();
    addListenerSpy = jest.fn();
    removeListenerSpy = jest.fn();

    // Mock matchMedia
    matchMediaMock = jest.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      addEventListener: addEventListenerSpy,
      removeEventListener: removeEventListenerSpy,
      addListener: addListenerSpy,
      removeListener: removeListenerSpy,
    }));

    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      configurable: true,
      value: matchMediaMock,
    });
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  it('should return false when user does not prefer reduced motion', () => {
    matchMediaMock.mockImplementation(() => ({
      matches: false,
      media: '(prefers-reduced-motion: reduce)',
      addEventListener: addEventListenerSpy,
      removeEventListener: removeEventListenerSpy,
    }));

    const { result } = renderHook(() => useReducedMotion());
    expect(result.current).toBe(false);
  });

  it('should return true when user prefers reduced motion', () => {
    matchMediaMock.mockImplementation(() => ({
      matches: true,
      media: '(prefers-reduced-motion: reduce)',
      addEventListener: addEventListenerSpy,
      removeEventListener: removeEventListenerSpy,
    }));

    const { result } = renderHook(() => useReducedMotion());
    expect(result.current).toBe(true);
  });

  it('should call matchMedia with correct query', () => {
    renderHook(() => useReducedMotion());
    expect(matchMediaMock).toHaveBeenCalledWith('(prefers-reduced-motion: reduce)');
  });

  it('should add event listener on mount (modern API)', () => {
    matchMediaMock.mockImplementation(() => ({
      matches: false,
      media: '(prefers-reduced-motion: reduce)',
      addEventListener: addEventListenerSpy,
      removeEventListener: removeEventListenerSpy,
    }));

    renderHook(() => useReducedMotion());
    expect(addEventListenerSpy).toHaveBeenCalledWith('change', expect.any(Function));
  });

  it('should remove event listener on unmount (modern API)', () => {
    matchMediaMock.mockImplementation(() => ({
      matches: false,
      media: '(prefers-reduced-motion: reduce)',
      addEventListener: addEventListenerSpy,
      removeEventListener: removeEventListenerSpy,
    }));

    const { unmount } = renderHook(() => useReducedMotion());
    unmount();
    expect(removeEventListenerSpy).toHaveBeenCalledWith('change', expect.any(Function));
  });

  it('should use legacy addListener if addEventListener not available', () => {
    matchMediaMock.mockImplementation(() => ({
      matches: false,
      media: '(prefers-reduced-motion: reduce)',
      addEventListener: undefined,
      removeEventListener: undefined,
      addListener: addListenerSpy,
      removeListener: removeListenerSpy,
    }));

    renderHook(() => useReducedMotion());
    expect(addListenerSpy).toHaveBeenCalledWith(expect.any(Function));
  });

  it('should use legacy removeListener on unmount if removeEventListener not available', () => {
    matchMediaMock.mockImplementation(() => ({
      matches: false,
      media: '(prefers-reduced-motion: reduce)',
      addEventListener: undefined,
      removeEventListener: undefined,
      addListener: addListenerSpy,
      removeListener: removeListenerSpy,
    }));

    const { unmount } = renderHook(() => useReducedMotion());
    unmount();
    expect(removeListenerSpy).toHaveBeenCalledWith(expect.any(Function));
  });

  it('should update when media query changes', () => {
    let changeHandler: ((event: MediaQueryListEvent) => void) | undefined;

    matchMediaMock.mockImplementation(() => ({
      matches: false,
      media: '(prefers-reduced-motion: reduce)',
      addEventListener: (event: string, handler: (event: MediaQueryListEvent) => void) => {
        changeHandler = handler;
      },
      removeEventListener: removeEventListenerSpy,
    }));

    const { result } = renderHook(() => useReducedMotion());
    expect(result.current).toBe(false);

    // Simulate media query change (within act to avoid warnings)
    if (changeHandler) {
      const { act } = require('@testing-library/react');
      act(() => {
        changeHandler!({ matches: true } as MediaQueryListEvent);
      });
    }

    expect(result.current).toBe(true);
  });

  it('should handle server-side rendering safely', () => {
    // Simulate SSR by removing window.matchMedia
    const originalMatchMedia = window.matchMedia;
    const originalWindow = global.window;

    // Temporarily remove window to simulate SSR
    delete (global as any).window;

    // Should not throw and should return false
    const { result } = renderHook(() => useReducedMotion());
    expect(result.current).toBe(false);

    // Restore
    (global as any).window = originalWindow;
    window.matchMedia = originalMatchMedia;
  });

  it('should return boolean type', () => {
    const { result } = renderHook(() => useReducedMotion());
    expect(typeof result.current).toBe('boolean');
  });

  it('should not re-create listeners on re-render', () => {
    const { rerender } = renderHook(() => useReducedMotion());

    const firstCallCount = addEventListenerSpy.mock.calls.length;

    // Force re-render
    rerender();

    const secondCallCount = addEventListenerSpy.mock.calls.length;

    // Should only add listener once (on mount)
    expect(firstCallCount).toBe(secondCallCount);
  });

  it('should handle multiple hook instances independently', () => {
    matchMediaMock.mockImplementation(() => ({
      matches: true,
      media: '(prefers-reduced-motion: reduce)',
      addEventListener: addEventListenerSpy,
      removeEventListener: removeEventListenerSpy,
    }));

    const { result: result1 } = renderHook(() => useReducedMotion());
    const { result: result2 } = renderHook(() => useReducedMotion());

    expect(result1.current).toBe(true);
    expect(result2.current).toBe(true);
    expect(result1.current).toBe(result2.current);
  });
});
