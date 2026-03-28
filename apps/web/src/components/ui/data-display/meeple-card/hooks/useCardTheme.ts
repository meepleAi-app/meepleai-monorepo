'use client';

import { useColorScheme } from '@/contexts/ColorSchemeContext';

/**
 * Detects if the Premium Gaming theme is active.
 * Returns 'gaming' or 'default'.
 *
 * Uses ColorSchemeContext as the single source of truth
 * instead of reading localStorage directly.
 */
export function useCardTheme(): 'gaming' | 'default' {
  try {
    const { currentTheme } = useColorScheme();
    return currentTheme.id.startsWith('gaming') ? 'gaming' : 'default';
  } catch {
    // Fallback if used outside ColorSchemeProvider
    return 'default';
  }
}
