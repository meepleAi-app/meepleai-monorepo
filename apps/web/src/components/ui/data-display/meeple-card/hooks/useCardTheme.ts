'use client';

import { useSafeColorScheme } from '@/contexts/ColorSchemeContext';

/**
 * Detects if the Premium Gaming theme is active.
 * Returns 'gaming' or 'default'.
 *
 * Uses ColorSchemeContext as the single source of truth
 * instead of reading localStorage directly.
 */
export function useCardTheme(): 'gaming' | 'default' {
  const colorScheme = useSafeColorScheme();
  if (colorScheme === undefined) return 'default';
  return colorScheme.currentTheme.id.startsWith('gaming') ? 'gaming' : 'default';
}
