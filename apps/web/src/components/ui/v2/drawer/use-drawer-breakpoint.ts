import { useSyncExternalStore } from 'react';

export type DrawerBreakpoint = 'mobile' | 'desktop';

const QUERY = '(min-width: 768px)';

type Win = (Window & typeof globalThis) | undefined;

export function resolveDrawerBreakpoint(win: Win): DrawerBreakpoint {
  if (!win || typeof win.matchMedia !== 'function') return 'mobile';
  return win.matchMedia(QUERY).matches ? 'desktop' : 'mobile';
}

export function getDrawerBreakpointSnapshot(): DrawerBreakpoint {
  return resolveDrawerBreakpoint(typeof window === 'undefined' ? undefined : window);
}

function subscribe(onChange: () => void): () => void {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return () => {};
  }
  const mql = window.matchMedia(QUERY);
  mql.addEventListener('change', onChange);
  return () => mql.removeEventListener('change', onChange);
}

function getServerSnapshot(): DrawerBreakpoint {
  return 'mobile';
}

export function useDrawerBreakpoint(): DrawerBreakpoint {
  return useSyncExternalStore(subscribe, () => getDrawerBreakpointSnapshot(), getServerSnapshot);
}
