/**
 * useScrollState Hook Tests
 * Mobile UX Epic — Issue 10
 *
 * Tests for the consolidated scroll state hook.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useScrollState } from '../useScrollState';

// ─── Setup ───────────────────────────────────────────────────────────────────

let scrollHandler: (() => void) | null = null;

beforeEach(() => {
  vi.useFakeTimers();
  scrollHandler = null;

  // Default scrollY
  Object.defineProperty(window, 'scrollY', { value: 0, writable: true, configurable: true });

  // Capture scroll listener
  vi.spyOn(window, 'addEventListener').mockImplementation((event, handler) => {
    if (event === 'scroll') {
      scrollHandler = handler as () => void;
    }
  });
  vi.spyOn(window, 'removeEventListener').mockImplementation(() => {});

  // Mock requestAnimationFrame to execute immediately
  vi.spyOn(window, 'requestAnimationFrame').mockImplementation(cb => {
    cb(0);
    return 0;
  });
  vi.spyOn(window, 'cancelAnimationFrame').mockImplementation(() => {});
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

function simulateScroll(y: number) {
  Object.defineProperty(window, 'scrollY', { value: y, writable: true, configurable: true });
  if (scrollHandler) {
    act(() => {
      scrollHandler!();
    });
  }
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('useScrollState', () => {
  it('returns initial state with null direction', () => {
    const { result } = renderHook(() => useScrollState());
    expect(result.current.direction).toBeNull();
    expect(result.current.isScrolled).toBe(false);
    expect(result.current.isScrolling).toBe(false);
    expect(result.current.scrollY).toBe(0);
  });

  it('registers a passive scroll listener', () => {
    renderHook(() => useScrollState());
    expect(window.addEventListener).toHaveBeenCalledWith('scroll', expect.any(Function), {
      passive: true,
    });
  });

  it('cleans up scroll listener on unmount', () => {
    const { unmount } = renderHook(() => useScrollState());
    unmount();
    expect(window.removeEventListener).toHaveBeenCalledWith('scroll', expect.any(Function));
  });

  it('detects scrolled state above threshold', () => {
    const { result } = renderHook(() => useScrollState({ scrolledThreshold: 4 }));

    simulateScroll(10);
    expect(result.current.isScrolled).toBe(true);
    expect(result.current.scrollY).toBe(10);
  });

  it('detects not scrolled below threshold', () => {
    const { result } = renderHook(() => useScrollState({ scrolledThreshold: 4 }));

    simulateScroll(2);
    expect(result.current.isScrolled).toBe(false);
  });

  it('detects scroll down direction above direction threshold', () => {
    const { result } = renderHook(() => useScrollState({ directionThreshold: 50 }));

    simulateScroll(60);
    expect(result.current.direction).toBe('down');
  });

  it('detects scroll up direction', () => {
    const { result } = renderHook(() => useScrollState({ directionThreshold: 50 }));

    // Scroll down first, then up
    simulateScroll(100);
    simulateScroll(40);
    expect(result.current.direction).toBe('up');
  });

  it('ignores scroll changes below direction threshold', () => {
    const { result } = renderHook(() => useScrollState({ directionThreshold: 50 }));

    simulateScroll(20);
    expect(result.current.direction).toBeNull();
  });

  it('sets isScrolling to true during scroll', () => {
    const { result } = renderHook(() => useScrollState());

    simulateScroll(100);
    expect(result.current.isScrolling).toBe(true);
  });

  it('sets isScrolling to false after timeout', () => {
    const { result } = renderHook(() => useScrollState({ scrollingTimeout: 150 }));

    simulateScroll(100);
    expect(result.current.isScrolling).toBe(true);

    act(() => {
      vi.advanceTimersByTime(200);
    });
    expect(result.current.isScrolling).toBe(false);
  });

  it('uses default thresholds when no options provided', () => {
    const { result } = renderHook(() => useScrollState());

    // Default directionThreshold is 50
    simulateScroll(30);
    expect(result.current.direction).toBeNull();

    simulateScroll(60);
    // 60 - 30 = 30, still below 50 threshold since lastScrollY was updated
    // Actually lastScrollY only updates when threshold is met
    // Initial lastScrollY is 0, so 60 - 0 = 60 >= 50
    expect(result.current.direction).toBe('down');
  });
});
