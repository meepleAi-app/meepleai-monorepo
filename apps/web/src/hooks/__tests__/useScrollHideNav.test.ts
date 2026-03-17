// apps/web/src/hooks/__tests__/useScrollHideNav.test.ts
import { renderHook, act } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

import { useScrollHideNav } from '../useScrollHideNav';

describe('useScrollHideNav', () => {
  let mockElement: HTMLDivElement;
  const listeners: Map<string, EventListener> = new Map();

  beforeEach(() => {
    mockElement = document.createElement('div');
    let _scrollTop = 0;
    Object.defineProperty(mockElement, 'scrollTop', {
      get: () => _scrollTop,
      set: (v: number) => {
        _scrollTop = v;
      },
      configurable: true,
    });

    vi.spyOn(mockElement, 'addEventListener').mockImplementation((event, handler) => {
      listeners.set(event as string, handler as EventListener);
    });
    vi.spyOn(mockElement, 'removeEventListener').mockImplementation(event => {
      listeners.delete(event as string);
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    listeners.clear();
  });

  function simulateScroll(newScrollTop: number) {
    mockElement.scrollTop = newScrollTop;
    const handler = listeners.get('scroll');
    if (handler) {
      act(() => {
        handler(new Event('scroll'));
      });
    }
  }

  it('returns isNavVisible=true initially', () => {
    const ref = { current: mockElement };
    const { result } = renderHook(() => useScrollHideNav({ scrollContainerRef: ref }));
    expect(result.current.isNavVisible).toBe(true);
    expect(result.current.scrollDirection).toBeNull();
  });

  it('hides nav on scroll down past threshold', () => {
    const ref = { current: mockElement };
    const { result } = renderHook(() =>
      useScrollHideNav({ scrollContainerRef: ref, threshold: 10 })
    );
    simulateScroll(20);
    expect(result.current.isNavVisible).toBe(false);
    expect(result.current.scrollDirection).toBe('down');
  });

  it('shows nav on scroll up past threshold', () => {
    const ref = { current: mockElement };
    const { result } = renderHook(() =>
      useScrollHideNav({ scrollContainerRef: ref, threshold: 10 })
    );
    simulateScroll(100);
    simulateScroll(80);
    expect(result.current.isNavVisible).toBe(true);
    expect(result.current.scrollDirection).toBe('up');
  });

  it('ignores scroll below threshold', () => {
    const ref = { current: mockElement };
    const { result } = renderHook(() =>
      useScrollHideNav({ scrollContainerRef: ref, threshold: 10 })
    );
    simulateScroll(5);
    expect(result.current.isNavVisible).toBe(true);
    expect(result.current.scrollDirection).toBeNull();
  });

  it('respects disabled option', () => {
    const ref = { current: mockElement };
    const { result } = renderHook(() =>
      useScrollHideNav({ scrollContainerRef: ref, disabled: true })
    );
    simulateScroll(100);
    expect(result.current.isNavVisible).toBe(true);
  });

  it('attaches listener to scrollContainerRef, not window', () => {
    const windowSpy = vi.spyOn(window, 'addEventListener');
    const ref = { current: mockElement };
    renderHook(() => useScrollHideNav({ scrollContainerRef: ref }));
    expect(mockElement.addEventListener).toHaveBeenCalledWith('scroll', expect.any(Function), {
      passive: true,
    });
    expect(windowSpy).not.toHaveBeenCalledWith('scroll', expect.any(Function), expect.anything());
    windowSpy.mockRestore();
  });

  it('cleans up listener on unmount', () => {
    const ref = { current: mockElement };
    const { unmount } = renderHook(() => useScrollHideNav({ scrollContainerRef: ref }));
    unmount();
    expect(mockElement.removeEventListener).toHaveBeenCalledWith('scroll', expect.any(Function));
  });
});
