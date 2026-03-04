/**
 * useUnifiedActionBar Hook
 * Issue #3479 - Layout System v2: Unified ActionBar
 *
 * Combines navigation items with context-specific actions for the unified bottom bar.
 * Handles responsive slot allocation and overflow management.
 */

'use client';

import { useEffect, useMemo, useCallback } from 'react';

import { usePathname, useRouter } from 'next/navigation';

import { useLayout } from '@/components/layout/LayoutProvider';
import { getActionsForContext } from '@/config/actions';
import {
  getNavItemsForBreakpoint,
  getOverflowNavItems,
  getContextActionSlots,
  isNavItemActive,
  type NavItem,
} from '@/config/navigation';
import type { Action, DeviceType } from '@/types/layout';

/**
 * Unified item that can be either a nav item or an action.
 */
export interface UnifiedItem {
  id: string;
  icon: string;
  label: string;
  ariaLabel?: string;
  type: 'nav' | 'action';
  isActive?: boolean;
  variant?: 'default' | 'primary' | 'destructive';
  href?: string;
  action?: string;
  testId?: string;
}

/**
 * Return type for useUnifiedActionBar hook.
 */
export interface UseUnifiedActionBarReturn {
  /** Navigation items visible in the bar */
  visibleNavItems: UnifiedItem[];
  /** Context actions visible in the bar */
  visibleContextActions: UnifiedItem[];
  /** All items in overflow menu (nav + actions) */
  overflowItems: UnifiedItem[];
  /** Whether overflow menu should be shown */
  hasOverflow: boolean;
  /** Whether the bar should be visible */
  isVisible: boolean;
  /** Current breakpoint */
  breakpoint: DeviceType;
  /** Handle item click (navigation or action) */
  handleItemClick: (item: UnifiedItem) => void;
}

/**
 * Convert NavItem to UnifiedItem format.
 */
function navItemToUnified(item: NavItem, isActive: boolean): UnifiedItem {
  return {
    id: item.id,
    icon: item.icon,
    label: item.label,
    ariaLabel: item.ariaLabel,
    type: 'nav',
    isActive,
    href: item.href,
    testId: item.testId,
  };
}

/**
 * Convert Action to UnifiedItem format.
 */
function actionToUnified(action: Action): UnifiedItem {
  return {
    id: action.id,
    icon: typeof action.icon === 'string' ? action.icon : 'circle',
    label: action.label,
    ariaLabel: action.ariaLabel,
    type: 'action',
    variant: action.variant,
    action: action.action,
  };
}

/**
 * Hook for unified action bar with navigation and context actions.
 *
 * @returns Object with visible items, overflow items, and handlers
 *
 * @example
 * ```tsx
 * const {
 *   visibleNavItems,
 *   visibleContextActions,
 *   overflowItems,
 *   hasOverflow,
 *   handleItemClick
 * } = useUnifiedActionBar();
 * ```
 */
export function useUnifiedActionBar(): UseUnifiedActionBarReturn {
  const pathname = usePathname();
  const router = useRouter();
  const { context, responsive, actionBar, isKeyboardVisible, isModalOpen } = useLayout();
  const { deviceType } = responsive;

  // Determine breakpoint for slot calculation
  const breakpoint = deviceType;

  // Get visible and overflow nav items
  const { visibleNavItems, overflowNavItems } = useMemo(() => {
    const visible = getNavItemsForBreakpoint(breakpoint);
    const overflow = getOverflowNavItems(breakpoint);

    return {
      visibleNavItems: visible.map(item =>
        navItemToUnified(item, isNavItemActive(item, pathname ?? ''))
      ),
      overflowNavItems: overflow.map(item =>
        navItemToUnified(item, isNavItemActive(item, pathname ?? ''))
      ),
    };
  }, [breakpoint, pathname]);

  // Get context actions for current context
  const { visibleContextActions, overflowContextActions } = useMemo(() => {
    // Use registered actions from context or fallback to config
    const contextActions =
      actionBar.actions.length > 0 ? actionBar.actions : getActionsForContext(context);

    const slots = getContextActionSlots(breakpoint);
    const visible = contextActions.slice(0, slots).map(actionToUnified);
    const overflow = contextActions.slice(slots).map(actionToUnified);

    return {
      visibleContextActions: visible,
      overflowContextActions: overflow,
    };
  }, [context, actionBar.actions, breakpoint]);

  // Combine overflow items
  const overflowItems = useMemo(() => {
    return [...overflowNavItems, ...overflowContextActions];
  }, [overflowNavItems, overflowContextActions]);

  // Determine if overflow menu is needed
  const hasOverflow = overflowItems.length > 0;

  // Determine visibility
  const isVisible = useMemo(() => {
    // Hide when keyboard is open or modal is open
    if (isKeyboardVisible || isModalOpen) {
      return false;
    }
    // Hide if actionBar is explicitly hidden
    if (!actionBar.visible) {
      return false;
    }
    return true;
  }, [isKeyboardVisible, isModalOpen, actionBar.visible]);

  // Handle item click
  const handleItemClick = useCallback(
    (item: UnifiedItem) => {
      if (item.type === 'nav' && item.href) {
        router.push(item.href);
      } else if (item.type === 'action' && item.action) {
        // Dispatch custom event for action handlers
        const event = new CustomEvent('actionbar:action', {
          detail: { action: item.action, itemId: item.id },
        });
        window.dispatchEvent(event);
      }
    },
    [router]
  );

  return {
    visibleNavItems,
    visibleContextActions,
    overflowItems,
    hasOverflow,
    isVisible,
    breakpoint,
    handleItemClick,
  };
}

/**
 * Hook to listen for action bar actions.
 *
 * @param callback - Function to call when an action is triggered
 *
 * @example
 * ```tsx
 * useUnifiedActionBarListener((action, itemId) => {
 *   if (action === 'library:add') {
 *     openAddGameModal();
 *   }
 * });
 * ```
 */
export function useUnifiedActionBarListener(callback: (action: string, itemId: string) => void) {
  const handleAction = useCallback(
    (event: Event) => {
      const customEvent = event as CustomEvent<{ action: string; itemId: string }>;
      callback(customEvent.detail.action, customEvent.detail.itemId);
    },
    [callback]
  );

  // Use effect to add/remove listener
  useEffect(() => {
    if (typeof window !== 'undefined') {
      window.addEventListener('actionbar:action', handleAction);
      return () => {
        window.removeEventListener('actionbar:action', handleAction);
      };
    }
  }, [handleAction]);
}
