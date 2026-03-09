/**
 * useVirtualKeyboard Hook Tests
 * Mobile UX Epic — Issue 13
 *
 * Tests for virtual keyboard detection via Visual Viewport API.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useVirtualKeyboard } from '../useVirtualKeyboard';

// ─── Setup ───────────────────────────────────────────────────────────────────

let resizeHandler: (() => void) | null = null;

function createMockVisualViewport(height: number) {
  const listeners: Record<string, Array<() => void>> = {};

  return {
    height,
    width: 375,
    offsetLeft: 0,
    offsetTop: 0,
    pageLeft: 0,
    pageTop: 0,
    scale: 1,
    addEventListener: vi.fn((event: string, handler: () => void) => {
      if (!listeners[event]) listeners[event] = [];
      listeners[event].push(handler);
      if (event === 'resize') resizeHandler = handler;
    }),
    removeEventListener: vi.fn((event: string, handler: () => void) => {
      if (listeners[event]) {
        listeners[event] = listeners[event].filter(h => h !== handler);
      }
    }),
    dispatchResize: (newHeight: number) => {
      (mockViewport as { height: number }).height = newHeight;
      listeners['resize']?.forEach(h => h());
    },
  };
}

let mockViewport: ReturnType<typeof createMockVisualViewport>;

beforeEach(() => {
  mockViewport = createMockVisualViewport(812);

  Object.defineProperty(window, 'innerHeight', {
    value: 812,
    writable: true,
    configurable: true,
  });

  Object.defineProperty(window, 'visualViewport', {
    value: mockViewport,
    writable: true,
    configurable: true,
  });
});

afterEach(() => {
  vi.restoreAllMocks();
  resizeHandler = null;
});

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('useVirtualKeyboard', () => {
  it('returns keyboard closed initially when viewport matches', () => {
    const { result } = renderHook(() => useVirtualKeyboard());
    expect(result.current.isKeyboardOpen).toBe(false);
    expect(result.current.keyboardHeight).toBe(0);
  });

  it('detects keyboard open when viewport shrinks significantly', () => {
    const { result } = renderHook(() => useVirtualKeyboard());

    act(() => {
      mockViewport.dispatchResize(500); // 812 - 500 = 312 > 150 threshold
    });

    expect(result.current.isKeyboardOpen).toBe(true);
    expect(result.current.keyboardHeight).toBe(312);
  });

  it('detects keyboard closed when viewport returns to full height', () => {
    const { result } = renderHook(() => useVirtualKeyboard());

    act(() => {
      mockViewport.dispatchResize(500);
    });
    expect(result.current.isKeyboardOpen).toBe(true);

    act(() => {
      mockViewport.dispatchResize(812);
    });
    expect(result.current.isKeyboardOpen).toBe(false);
    expect(result.current.keyboardHeight).toBe(0);
  });

  it('ignores small viewport changes below threshold', () => {
    const { result } = renderHook(() => useVirtualKeyboard());

    act(() => {
      mockViewport.dispatchResize(750); // 812 - 750 = 62 < 150 threshold
    });

    expect(result.current.isKeyboardOpen).toBe(false);
    expect(result.current.keyboardHeight).toBe(0);
  });

  it('registers resize and scroll listeners on visualViewport', () => {
    renderHook(() => useVirtualKeyboard());

    expect(mockViewport.addEventListener).toHaveBeenCalledWith('resize', expect.any(Function));
    expect(mockViewport.addEventListener).toHaveBeenCalledWith('scroll', expect.any(Function));
  });

  it('cleans up listeners on unmount', () => {
    const { unmount } = renderHook(() => useVirtualKeyboard());
    unmount();

    expect(mockViewport.removeEventListener).toHaveBeenCalledWith('resize', expect.any(Function));
    expect(mockViewport.removeEventListener).toHaveBeenCalledWith('scroll', expect.any(Function));
  });

  it('handles missing visualViewport gracefully', () => {
    Object.defineProperty(window, 'visualViewport', {
      value: null,
      writable: true,
      configurable: true,
    });

    const { result } = renderHook(() => useVirtualKeyboard());
    expect(result.current.isKeyboardOpen).toBe(false);
    expect(result.current.keyboardHeight).toBe(0);
  });
});
