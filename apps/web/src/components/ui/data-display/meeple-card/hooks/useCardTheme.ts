'use client';

import { useEffect, useState } from 'react';

/**
 * Detects if the Premium Gaming theme is active.
 * Returns 'gaming' or 'default'.
 */
export function useCardTheme(): 'gaming' | 'default' {
  const [theme, setTheme] = useState<'gaming' | 'default'>('default');

  useEffect(() => {
    const stored = localStorage.getItem('meepleai-theme');
    if (stored) {
      try {
        const parsed = JSON.parse(stored);
        if (parsed?.id?.startsWith('gaming')) {
          setTheme('gaming');
        }
      } catch {
        // ignore parse errors
      }
    }
  }, []);

  return theme;
}
