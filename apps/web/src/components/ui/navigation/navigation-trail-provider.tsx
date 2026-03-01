/**
 * NavigationTrailProvider - Context provider for breadcrumb trail
 *
 * Wraps useNavigationTrail in a React context so any descendant component
 * can read or modify the navigation trail. Integrates with Next.js router
 * to auto-clear the trail on direct URL navigation (i.e. when the user
 * types a URL or uses browser back/forward instead of in-app nav links).
 *
 * @see Issue #4704
 * @see Epic #4688 - MeepleCard Navigation System
 */

'use client';

import { createContext, useContext, useEffect, useRef } from 'react';

import { usePathname } from 'next/navigation';

import {
  useNavigationTrail,
  type UseNavigationTrailReturn,
} from '@/hooks/use-navigation-trail';

// ---------------------------------------------------------------------------
// Context
// ---------------------------------------------------------------------------

const NavigationTrailContext = createContext<UseNavigationTrailReturn | null>(
  null,
);

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

/**
 * Access the navigation trail context.
 * Must be used within a `<NavigationTrailProvider>`.
 */
export function useNavigationTrailContext(): UseNavigationTrailReturn {
  const ctx = useContext(NavigationTrailContext);
  if (!ctx) {
    throw new Error(
      'useNavigationTrailContext must be used within <NavigationTrailProvider>',
    );
  }
  return ctx;
}

// ---------------------------------------------------------------------------
// Provider
// ---------------------------------------------------------------------------

interface NavigationTrailProviderProps {
  children: React.ReactNode;
}

export function NavigationTrailProvider({
  children,
}: NavigationTrailProviderProps) {
  const trailApi = useNavigationTrail();
  const pathname = usePathname();
  const prevPathname = useRef(pathname);

  // Detect direct URL navigation (not triggered by in-app nav links).
  // When pathname changes and the trail's last step doesn't match the new
  // path, it means the user navigated directly — clear the trail.
  useEffect(() => {
    if (pathname === prevPathname.current) return;
    prevPathname.current = pathname;

    const { trail } = trailApi;
    if (trail.length === 0) return;

    const lastHref = trail[trail.length - 1].href;
    // If the new pathname matches the trail's last href, the navigation
    // was triggered by in-app link — keep the trail intact.
    if (pathname === lastHref || pathname.startsWith(lastHref + '/')) return;

    // Check if any step matches — if not, user navigated away entirely
    const matchesAnyStep = trail.some(
      (s) => pathname === s.href || pathname.startsWith(s.href + '/'),
    );

    if (!matchesAnyStep) {
      trailApi.clear();
    }
  }, [pathname, trailApi]);

  return (
    <NavigationTrailContext.Provider value={trailApi}>
      {children}
    </NavigationTrailContext.Provider>
  );
}
