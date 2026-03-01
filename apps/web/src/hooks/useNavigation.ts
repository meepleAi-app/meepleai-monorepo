'use client';

/**
 * useNavigation — consume the current page navigation config.
 * Issue #5034 - Navigation Config System
 *
 * Re-export from NavigationContext for convenience.
 * Provides MiniNav tabs and ActionBar actions to layout components.
 *
 * @example
 *   const { miniNavTabs, actionBarActions, activeZone } = useNavigation();
 */

export { useNavigation } from '@/context/NavigationContext';
export type { NavigationContextValue } from '@/types/navigation';
