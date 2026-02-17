/**
 * useNavigationItems Hook
 * Provides filtered navigation items based on user auth state and role.
 *
 * All navigation consumers should use this hook instead of importing
 * UNIFIED_NAV_ITEMS or NAV_ITEMS directly.
 */

'use client';

import { useMemo } from 'react';

import {
  UNIFIED_NAV_ITEMS,
  filterNavItemsByRole,
  isUnifiedNavItemActive,
} from '@/config/navigation';
import type { UnifiedNavItem } from '@/config/navigation.types';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

export interface UseNavigationItemsReturn {
  /** Filtered navigation items for the current user */
  items: UnifiedNavItem[];
  /** Whether the auth state is still loading */
  isAuthLoading: boolean;
  /** Whether the user is authenticated */
  isAuthenticated: boolean;
  /** Check if a path matches a navigation item */
  isItemActive: (item: UnifiedNavItem, pathname: string) => boolean;
}

/**
 * Hook that provides navigation items filtered by the current user's
 * authentication state and role.
 *
 * @example
 * ```tsx
 * const { items, isAuthLoading } = useNavigationItems();
 * // Render items in header/drawer/etc.
 * ```
 */
export function useNavigationItems(): UseNavigationItemsReturn {
  const { data: user, isLoading: isAuthLoading } = useCurrentUser();

  const isAuthenticated = !!user;

  const items = useMemo(
    () =>
      filterNavItemsByRole(UNIFIED_NAV_ITEMS, {
        isAuthenticated,
        isAuthLoading,
        userRole: user?.role ?? null,
      }).filter(item => !item.hideFromMainNav),
    [isAuthenticated, isAuthLoading, user?.role]
  );

  return {
    items,
    isAuthLoading,
    isAuthenticated,
    isItemActive: isUnifiedNavItemActive,
  };
}
