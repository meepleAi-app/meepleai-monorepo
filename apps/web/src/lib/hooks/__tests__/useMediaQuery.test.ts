/**
 * useMediaQuery hook tests.
 *
 * Issue #1488: Play Records wizard responsive split (Task 3)
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useMediaQuery } from '../useMediaQuery';

// Minimal mock for MediaQueryList so tests don't depend on jsdom's default stub.
function createMqlStub(matches: boolean) {
  const listeners: Array<(e: MediaQueryListEvent) => void> = [];
  const stub = {
    matches,
    media: '',
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: (type: string, handler: (e: MediaQueryListEvent) => void) => {
      listeners.push(handler);
    },
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
    _fire(newMatches: boolean) {
      stub.matches = newMatches;
      const e = { matches: newMatches } as MediaQueryListEvent;
      listeners.forEach(l => l(e));
    },
  };
  return stub;
}

describe('useMediaQuery', () => {
  let originalMatchMedia: typeof window.matchMedia;

  beforeEach(() => {
    originalMatchMedia = window.matchMedia;
  });

  afterEach(() => {
    window.matchMedia = originalMatchMedia;
    vi.restoreAllMocks();
  });

  it('returns false initially (SSR-safe default)', () => {
    const stub = createMqlStub(false);
    window.matchMedia = vi.fn().mockReturnValue(stub);

    const { result } = renderHook(() => useMediaQuery('(max-width: 768px)'));
    // First render: false (SSR default)
    expect(result.current).toBe(false);
  });

  it('returns true when media query matches on mount', async () => {
    const stub = createMqlStub(true);
    window.matchMedia = vi.fn().mockReturnValue(stub);

    const { result } = renderHook(() => useMediaQuery('(max-width: 768px)'));
    // After useEffect fires, should reflect stub.matches = true
    await act(async () => {});
    expect(result.current).toBe(true);
  });

  it('returns false when media query does not match on mount', async () => {
    const stub = createMqlStub(false);
    window.matchMedia = vi.fn().mockReturnValue(stub);

    const { result } = renderHook(() => useMediaQuery('(min-width: 769px)'));
    await act(async () => {});
    expect(result.current).toBe(false);
  });

  it('updates when the media query match state changes', async () => {
    const stub = createMqlStub(false);
    window.matchMedia = vi.fn().mockReturnValue(stub);

    const { result } = renderHook(() => useMediaQuery('(max-width: 768px)'));
    await act(async () => {});
    expect(result.current).toBe(false);

    // Simulate a resize that triggers the MQL
    await act(async () => {
      stub._fire(true);
    });
    expect(result.current).toBe(true);
  });

  it('calls matchMedia with the provided query string', async () => {
    const stub = createMqlStub(false);
    const matchMediaSpy = vi.fn().mockReturnValue(stub);
    window.matchMedia = matchMediaSpy;

    renderHook(() => useMediaQuery('(max-width: 768px)'));
    await act(async () => {});

    expect(matchMediaSpy).toHaveBeenCalledWith('(max-width: 768px)');
  });

  it('registers change event listener', async () => {
    const stub = createMqlStub(false);
    const addEventListenerSpy = vi.spyOn(stub, 'addEventListener');
    window.matchMedia = vi.fn().mockReturnValue(stub);

    renderHook(() => useMediaQuery('(max-width: 768px)'));
    await act(async () => {});

    expect(addEventListenerSpy).toHaveBeenCalledWith('change', expect.any(Function));
  });

  it('removes change event listener on unmount', async () => {
    const stub = createMqlStub(false);
    const removeEventListenerSpy = vi.spyOn(stub, 'removeEventListener');
    window.matchMedia = vi.fn().mockReturnValue(stub);

    const { unmount } = renderHook(() => useMediaQuery('(max-width: 768px)'));
    await act(async () => {});
    unmount();

    expect(removeEventListenerSpy).toHaveBeenCalledWith('change', expect.any(Function));
  });
});
