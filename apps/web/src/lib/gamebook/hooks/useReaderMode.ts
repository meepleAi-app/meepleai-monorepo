'use client';

import { useCallback, useEffect, useState } from 'react';

const STORAGE_KEY = 'reader-mode-enabled';

export interface UseReaderModeResult {
  /** Current reader-mode state (true = active, larger font + line-height + max-width). */
  isReaderMode: boolean;
  /** Flip reader-mode state. Stable reference across renders. */
  toggle: () => void;
}

/**
 * SSR-safe reader-mode hook backed by localStorage (key: `reader-mode-enabled`).
 *
 * Per DEC-3 + DEC-9: gracefully degrades to in-memory state if localStorage
 * is unavailable (private mode, quota exceeded). Returns false on SSR; reads
 * actual value after hydration to avoid mismatch warnings.
 *
 * #1558 H · Aaron CORE spec 2026-05-23 §1b.
 */
export function useReaderMode(): UseReaderModeResult {
  const [isReaderMode, setIsReaderMode] = useState(false);

  useEffect(() => {
    if (typeof window === 'undefined') return;
    try {
      const stored = window.localStorage.getItem(STORAGE_KEY);
      if (stored === 'true') setIsReaderMode(true);
    } catch {
      // localStorage unavailable (private mode / SecurityError) — keep default false
    }
  }, []);

  const toggle = useCallback(() => {
    setIsReaderMode(prev => {
      const next = !prev;
      if (typeof window !== 'undefined') {
        try {
          window.localStorage.setItem(STORAGE_KEY, String(next));
        } catch {
          // localStorage write blocked — in-memory state still flips correctly
        }
      }
      return next;
    });
  }, []);

  return { isReaderMode, toggle };
}
