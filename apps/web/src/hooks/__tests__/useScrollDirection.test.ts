/**
 * Tests for useScrollDirection hook
 * Issue #4 from mobile-first-ux-epic.md
 */

import { renderHook, act } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

import { useScrollDirection } from '../useScrollDirection';

describe('useScrollDirection', () => {
  let scrollY: number;
  const listeners: Map<string, EventListener> = new Map();

  beforeEach(() => {
    scrollY = 0;
    Object.defineProperty(window, 'scrollY', {
      get: () => scrollY,
      configurable: true,
    });

    vi.spyOn(window, 'addEventListener').mockImplementation((event, handler) => {
      listeners.set(event, handler as EventListener);
    });

    vi.spyOn(window, 'removeEventListener').mockImplementation(event => {
      listeners.delete(event);
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    listeners.clear();
  });

  function simulateScroll(newY: number) {
    scrollY = newY;
    const handler = listeners.get('scroll');
    if (handler) {
      act(() => {
        handler(new Event('scroll'));
      });
    }
  }

  it('returns null initially', () => {
    const { result } = renderHook(() => useScrollDirection());
    expect(result.current).toBeNull();
  });

  it('returns "down" after scrolling down past threshold', () => {
    const { result } = renderHook(() => useScrollDirection({ threshold: 50 }));
    simulateScroll(60);
    expect(result.current).toBe('down');
  });

  it('returns "up" after scrolling up past threshold', () => {
    const { result } = renderHook(() => useScrollDirection({ threshold: 50 }));
    simulateScroll(200);
    simulateScroll(100);
    expect(result.current).toBe('up');
  });

  it('does not change direction for scroll less than threshold', () => {
    const { result } = renderHook(() => useScrollDirection({ threshold: 50 }));
    simulateScroll(30);
    expect(result.current).toBeNull();
  });

  it('uses passive scroll listener', () => {
    renderHook(() => useScrollDirection());
    expect(window.addEventListener).toHaveBeenCalledWith('scroll', expect.any(Function), {
      passive: true,
    });
  });

  it('cleans up scroll listener on unmount', () => {
    const { unmount } = renderHook(() => useScrollDirection());
    unmount();
    expect(window.removeEventListener).toHaveBeenCalledWith('scroll', expect.any(Function));
  });

  it('defaults threshold to 50', () => {
    const { result } = renderHook(() => useScrollDirection());
    simulateScroll(40);
    expect(result.current).toBeNull();
    simulateScroll(60);
    expect(result.current).toBe('down');
  });
});
