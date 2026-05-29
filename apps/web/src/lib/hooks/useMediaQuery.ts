/**
 * useMediaQuery — SSR-safe media query hook.
 *
 * Returns `true` when the document matches the given CSS media query string.
 * On the server (SSR/SSG) it always returns `false` to avoid hydration
 * mismatches; the real value is available after the first client-side paint.
 *
 * Usage:
 *   const isMobile = useMediaQuery('(max-width: 768px)');
 *
 * Issue #1488: Play Records wizard responsive split (Task 3)
 */
'use client';

import { useEffect, useState } from 'react';

export function useMediaQuery(query: string): boolean {
  // During SSR window is undefined — default to false.
  const [matches, setMatches] = useState<boolean>(false);

  useEffect(() => {
    if (typeof window === 'undefined') return;

    const mql = window.matchMedia(query);

    // Initialise with the current state after mount.
    setMatches(mql.matches);

    // Update on change.
    const handler = (event: MediaQueryListEvent) => {
      setMatches(event.matches);
    };

    mql.addEventListener('change', handler);
    return () => {
      mql.removeEventListener('change', handler);
    };
  }, [query]);

  return matches;
}
