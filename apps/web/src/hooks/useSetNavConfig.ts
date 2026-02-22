'use client';

/**
 * useSetNavConfig — set the current page navigation configuration.
 * Issue #5034 - Navigation Config System
 *
 * Re-export from NavigationContext for convenience.
 * Used by page layouts to declare their MiniNav tabs and ActionBar actions.
 *
 * @example
 *   const setNavConfig = useSetNavConfig();
 *   useEffect(() => {
 *     setNavConfig({
 *       miniNav: [{ id: 'games', label: 'Games', href: '/library' }],
 *       actionBar: [{ id: 'add', label: 'Add Game', icon: PlusIcon, onClick: handleAdd, variant: 'primary' }],
 *     });
 *   }, []);
 */

export { useSetNavConfig } from '@/context/NavigationContext';
export type { PageNavConfig, NavTab, NavAction } from '@/types/navigation';
