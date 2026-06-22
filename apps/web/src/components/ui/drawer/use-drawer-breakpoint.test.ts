import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useDrawerBreakpoint } from './use-drawer-breakpoint';

type Listener = (e: MediaQueryListEvent) => void;

interface MockMQL {
  matches: boolean;
  media: string;
  addEventListener: (event: 'change', cb: Listener) => void;
  removeEventListener: (event: 'change', cb: Listener) => void;
  // legacy API kept for safety
  addListener?: (cb: Listener) => void;
  removeListener?: (cb: Listener) => void;
  dispatchEvent?: (e: Event) => boolean;
  onchange: Listener | null;
}

function installMatchMedia(initialMatches: boolean) {
  const listeners = new Set<Listener>();
  const mql: MockMQL = {
    matches: initialMatches,
    media: '(min-width: 768px)',
    addEventListener: (_e, cb) => listeners.add(cb),
    removeEventListener: (_e, cb) => listeners.delete(cb),
    onchange: null,
  };
  const matchMedia = vi.fn().mockReturnValue(mql);
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: matchMedia,
  });
  return {
    mql,
    fire(matches: boolean) {
      mql.matches = matches;
      const evt = { matches } as MediaQueryListEvent;
      listeners.forEach(cb => cb(evt));
    },
  };
}

describe('useDrawerBreakpoint', () => {
  let originalMatchMedia: typeof window.matchMedia | undefined;

  beforeEach(() => {
    originalMatchMedia = window.matchMedia;
  });

  afterEach(() => {
    if (originalMatchMedia) {
      Object.defineProperty(window, 'matchMedia', {
        configurable: true,
        writable: true,
        value: originalMatchMedia,
      });
    }
    vi.restoreAllMocks();
  });

  it('returns "mobile" when (min-width: 768px) does not match', () => {
    installMatchMedia(false);
    const { result } = renderHook(() => useDrawerBreakpoint());
    expect(result.current).toBe('mobile');
  });

  it('returns "desktop" when (min-width: 768px) matches', () => {
    installMatchMedia(true);
    const { result } = renderHook(() => useDrawerBreakpoint());
    expect(result.current).toBe('desktop');
  });

  it('updates when matchMedia change event fires', () => {
    const m = installMatchMedia(false);
    const { result } = renderHook(() => useDrawerBreakpoint());
    expect(result.current).toBe('mobile');
    act(() => {
      m.fire(true);
    });
    expect(result.current).toBe('desktop');
    act(() => {
      m.fire(false);
    });
    expect(result.current).toBe('mobile');
  });

  it('is SSR safe — defaults to "mobile" when window is undefined', async () => {
    // jsdom always defines `window`, so exercise the resolver directly.
    const mod = await import('./use-drawer-breakpoint');
    expect(mod.resolveDrawerBreakpoint(undefined)).toBe('mobile');
  });

  it('returns "mobile" when matchMedia is missing on window', async () => {
    // Simulate environment without matchMedia (e.g. older SSR shims).
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      writable: true,
      value: undefined,
    });
    const mod = await import('./use-drawer-breakpoint');
    expect(mod.getDrawerBreakpointSnapshot()).toBe('mobile');
  });
});
