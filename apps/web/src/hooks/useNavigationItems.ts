/**
 * useNavigationItems Hook
 * Provides navigation items filtered by the current user's auth state and role,
 * pre-split per sp4 navbar surface (top bar / bottom tabs / overflow).
 *
 * All navigation consumers should use this hook instead of importing
 * UNIFIED_NAV_ITEMS or NAV_ITEMS directly.
 */

'use client';

import { useMemo } from 'react';

import {
  BOTTOM_TAB_NAV_IDS,
  TOP_BAR_NAV_IDS,
  UNIFIED_NAV_ITEMS,
  USER_PILL_NAV_IDS,
  filterNavItemsByRole,
  isUnifiedNavItemActive,
  pickNavItemsByIds,
} from '@/config/navigation';
import type { UnifiedNavItem } from '@/config/navigation.types';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

export interface UseNavigationItemsReturn {
  /** Role-filtered items minus those hidden from the main nav (legacy consumers). */
  items: UnifiedNavItem[];
  /** Ordered items for the desktop top-bar primary link row. */
  topBarItems: UnifiedNavItem[];
  /** Items for the desktop "Altro" overflow dropdown (not in top row, not in user pill). */
  desktopOverflowItems: UnifiedNavItem[];
  /** Ordered items for the fixed mobile bottom-bar tabs. */
  bottomTabItems: UnifiedNavItem[];
  /** Items for the mobile ☰ drawer ("tutto il resto": not a bottom tab, not the bell). */
  mobileDrawerItems: UnifiedNavItem[];
  /** Whether the auth state is still loading. */
  isAuthLoading: boolean;
  /** Whether the user is authenticated. */
  isAuthenticated: boolean;
  /** Check if a path matches a navigation item. */
  isItemActive: (item: UnifiedNavItem, pathname: string) => boolean;
}

/**
 * Hook that provides navigation items filtered by the current user's
 * authentication state and role, split per sp4 navbar surface.
 *
 * @example
 * ```tsx
 * const { topBarItems, bottomTabItems, isItemActive } = useNavigationItems();
 * ```
 */
export function useNavigationItems(): UseNavigationItemsReturn {
  const { data: user, isLoading: isAuthLoading } = useCurrentUser();

  const isAuthenticated = !!user;
  const userRole = user?.role ?? null;

  // Full role-filtered set — NOT stripped of `hideFromMainNav`, because some
  // hidden items are surfaced on specific surfaces (e.g. Profilo as a bottom tab).
  const fullItems = useMemo(
    () => filterNavItemsByRole(UNIFIED_NAV_ITEMS, { isAuthenticated, isAuthLoading, userRole }),
    [isAuthenticated, isAuthLoading, userRole]
  );

  // Backward-compatible list (legacy consumers): role-filtered minus hidden.
  const items = useMemo(() => fullItems.filter(item => !item.hideFromMainNav), [fullItems]);

  const topBarItems = useMemo(() => pickNavItemsByIds(fullItems, TOP_BAR_NAV_IDS), [fullItems]);

  const bottomTabItems = useMemo(
    () => pickNavItemsByIds(fullItems, BOTTOM_TAB_NAV_IDS),
    [fullItems]
  );

  const desktopOverflowItems = useMemo(() => {
    const exclude = new Set<string>([...TOP_BAR_NAV_IDS, ...USER_PILL_NAV_IDS, 'welcome']);
    return fullItems.filter(item => !exclude.has(item.id));
  }, [fullItems]);

  const mobileDrawerItems = useMemo(() => {
    const exclude = new Set<string>([...BOTTOM_TAB_NAV_IDS, 'notifications', 'welcome']);
    return fullItems.filter(item => !exclude.has(item.id));
  }, [fullItems]);

  return {
    items,
    topBarItems,
    desktopOverflowItems,
    bottomTabItems,
    mobileDrawerItems,
    isAuthLoading,
    isAuthenticated,
    isItemActive: isUnifiedNavItemActive,
  };
}
