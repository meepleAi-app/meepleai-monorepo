/**
 * useViewMode — client hook for view mode toggle state.
 *
 * Reads the `meepleai_view_mode` cookie on mount, exposes `viewMode` + `toggle`.
 * On toggle: flips the value, writes the cookie, navigates to the opposite shell.
 *
 * Path-based default: if no cookie, defaults to 'admin' on /admin/* paths,
 * 'user' elsewhere. This matches the shell the user is currently viewing.
 *
 * Cross-tab sync is intentionally NOT implemented: document.cookie has no
 * change event, and BroadcastChannel is overkill for a UX preference.
 * See spec §4.1 and the plan's "Known limitations" section.
 */
'use client';

import { useCallback, useState } from 'react';

import { usePathname, useRouter } from 'next/navigation';

import type { ViewMode } from '@/lib/view-mode/constants';
import { readViewModeCookie, writeViewModeCookie } from '@/lib/view-mode/cookie';

interface UseViewModeResult {
  viewMode: ViewMode;
  toggle: () => void;
}

/**
 * Default view mode inferred from URL path.
 */
function defaultViewMode(pathname: string): ViewMode {
  return pathname.startsWith('/admin') ? 'admin' : 'user';
}

export function useViewMode(): UseViewModeResult {
  const router = useRouter();
  const pathname = usePathname();

  // Initial state reads cookie synchronously (matches SSR when cookie exists)
  const [viewMode, setViewMode] = useState<ViewMode>(() => {
    if (typeof document === 'undefined') return defaultViewMode(pathname);
    return readViewModeCookie() ?? defaultViewMode(pathname);
  });

  const toggle = useCallback(() => {
    const next: ViewMode = viewMode === 'admin' ? 'user' : 'admin';
    writeViewModeCookie(next);
    setViewMode(next);
    router.push(next === 'admin' ? '/admin/overview' : '/library');
  }, [viewMode, router]);

  return { viewMode, toggle };
}
