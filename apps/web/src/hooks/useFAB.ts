/**
 * useFAB Hook
 * Issue #3291 - Phase 5: Smart FAB
 *
 * Manages FAB visibility and configuration based on layout context.
 */

'use client';

import { useState, useCallback, useMemo, useEffect } from 'react';

import { useLayout } from '@/components/layout/LayoutProvider';
import { getFABConfig, type FABActionConfig } from '@/config/fab';

/**
 * Hook return type
 */
export interface UseFABReturn {
  /** Whether FAB should be visible */
  isVisible: boolean;
  /** Current FAB configuration */
  config: FABActionConfig | null;
  /** Whether quick menu is open */
  isQuickMenuOpen: boolean;
  /** Open quick menu */
  openQuickMenu: () => void;
  /** Close quick menu */
  closeQuickMenu: () => void;
  /** Toggle quick menu */
  toggleQuickMenu: () => void;
  /** Trigger primary FAB action */
  triggerAction: () => void;
  /** Trigger quick menu item action */
  triggerQuickAction: (actionId: string) => void;
}

/**
 * useFAB Hook
 *
 * Provides FAB visibility and action handling based on current context.
 * FAB is only visible on mobile and when conditions allow.
 */
export function useFAB(): UseFABReturn {
  const { context, responsive, fab, isKeyboardVisible, isModalOpen, scrollDirection } = useLayout();
  const { isMobile } = responsive;

  const [isQuickMenuOpen, setIsQuickMenuOpen] = useState(false);

  // Get FAB config for current context
  const config = useMemo(() => {
    return getFABConfig(context);
  }, [context]);

  // Calculate visibility
  const isVisible = useMemo(() => {
    // Only show on mobile
    if (!isMobile) return false;

    // Must have a config for this context
    if (!config) return false;

    // Check LayoutProvider FAB state
    if (!fab.visible) return false;

    // Hide when keyboard is open
    if (isKeyboardVisible) return false;

    // Hide when modal is open
    if (isModalOpen) return false;

    // Hide during fast scrolling down
    if (scrollDirection === 'down') return false;

    return true;
  }, [isMobile, config, fab.visible, isKeyboardVisible, isModalOpen, scrollDirection]);

  // Close quick menu when context changes or FAB becomes invisible
  useEffect(() => {
    if (!isVisible) {
      setIsQuickMenuOpen(false);
    }
  }, [isVisible, context]);

  // Quick menu handlers
  const openQuickMenu = useCallback(() => {
    setIsQuickMenuOpen(true);
  }, []);

  const closeQuickMenu = useCallback(() => {
    setIsQuickMenuOpen(false);
  }, []);

  const toggleQuickMenu = useCallback(() => {
    setIsQuickMenuOpen(prev => !prev);
  }, []);

  // Action handlers
  const triggerAction = useCallback(() => {
    if (!config) return;

    // Dispatch action event
    const event = new CustomEvent('fab:action', {
      detail: { action: config.action },
    });
    window.dispatchEvent(event);
  }, [config]);

  const triggerQuickAction = useCallback((actionId: string) => {
    if (!config) return;

    const item = config.quickMenuItems.find(i => i.id === actionId);
    if (!item) return;

    // Close quick menu
    setIsQuickMenuOpen(false);

    // Dispatch action event
    const event = new CustomEvent('fab:quickaction', {
      detail: { action: item.action, actionId },
    });
    window.dispatchEvent(event);
  }, [config]);

  return {
    isVisible,
    config,
    isQuickMenuOpen,
    openQuickMenu,
    closeQuickMenu,
    toggleQuickMenu,
    triggerAction,
    triggerQuickAction,
  };
}
