'use client';

/**
 * View-mode hook for the `/library` desktop surface (Issue #574, Wave B.3).
 *
 * 'grid' | 'list' | 'compact' — the third option supports the hybrid grid's
 * three layouts. The hook persists the choice in `localStorage` so the user
 * keeps their preferred density across sessions.
 */

import { useCallback, useEffect, useState } from 'react';

export type LibraryViewMode = 'grid' | 'list' | 'compact';

const STORAGE_KEY = 'mai-library-view';
const VALID_MODES: readonly LibraryViewMode[] = ['grid', 'list', 'compact'];

function isValidMode(value: unknown): value is LibraryViewMode {
  return typeof value === 'string' && VALID_MODES.includes(value as LibraryViewMode);
}

export interface UseLibraryViewResult {
  view: LibraryViewMode;
  setView: (next: LibraryViewMode) => void;
}

export function useLibraryView(initial: LibraryViewMode = 'grid'): UseLibraryViewResult {
  const [view, setViewState] = useState<LibraryViewMode>(initial);

  // Hydrate from localStorage on the client only (avoid SSR mismatch).
  useEffect(() => {
    if (typeof window === 'undefined') return;
    try {
      const stored = window.localStorage.getItem(STORAGE_KEY);
      if (isValidMode(stored)) {
        setViewState(stored);
      } else if (stored != null && !isValidMode(stored)) {
        // Sanitize legacy or corrupted values without crashing.
        window.localStorage.removeItem(STORAGE_KEY);
      }
    } catch {
      // localStorage may throw in privacy modes / quota / SecurityError — swallow.
    }
  }, []);

  const setView = useCallback((next: LibraryViewMode): void => {
    setViewState(next);
    if (typeof window === 'undefined') return;
    try {
      window.localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // Quota or privacy errors should not break the UI; the in-memory state
      // is still updated, the choice just won't persist across reloads.
    }
  }, []);

  return { view, setView };
}
