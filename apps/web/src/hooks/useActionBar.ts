/**
 * useActionBar Hook
 * Issue #3290 - Phase 4: ActionBar System
 *
 * Manages ActionBar state including visible and overflow actions
 * based on current context and device type.
 */

'use client';

import { useMemo } from 'react';

import { useLayout } from '@/components/layout/LayoutProvider';
import { getActionsForContext, getMaxVisibleActions } from '@/config/actions';
import type { Action } from '@/types/layout';

/**
 * Hook return type
 */
export interface UseActionBarReturn {
  /** Actions visible in the main action bar */
  visibleActions: Action[];
  /** Actions that overflow to the dropdown menu */
  overflowActions: Action[];
  /** Whether the action bar is empty (no actions) */
  isEmpty: boolean;
  /** Whether there are overflow actions */
  hasOverflow: boolean;
  /** Current context */
  context: string;
  /** Whether the action bar is visible */
  isVisible: boolean;
}

/**
 * useActionBar Hook
 *
 * Provides computed actions for the ActionBar based on current context
 * and device type. Actions are split into visible and overflow groups.
 */
export function useActionBar(): UseActionBarReturn {
  const { context, responsive, actionBar } = useLayout();
  const { deviceType } = responsive;

  // Get all actions for current context
  const allActions = useMemo(() => {
    return getActionsForContext(context);
  }, [context]);

  // Calculate max visible based on device type
  const maxVisible = useMemo(() => {
    return getMaxVisibleActions(deviceType);
  }, [deviceType]);

  // Split actions into visible and overflow
  const { visibleActions, overflowActions } = useMemo(() => {
    // Filter out disabled actions from visible (but keep in overflow)
    const enabledActions = allActions.filter(
      (action) => !action.isDisabled
    );

    const visible = enabledActions.slice(0, maxVisible);
    const overflow = enabledActions.slice(maxVisible);

    return { visibleActions: visible, overflowActions: overflow };
  }, [allActions, maxVisible]);

  // Check if action bar should be visible
  const isVisible = useMemo(() => {
    // Hide if explicitly set to invisible
    if (!actionBar.visible) return false;
    // Show if there are any actions
    return allActions.length > 0;
  }, [actionBar.visible, allActions.length]);

  return {
    visibleActions,
    overflowActions,
    isEmpty: allActions.length === 0,
    hasOverflow: overflowActions.length > 0,
    context,
    isVisible,
  };
}

/**
 * useActionBarAction Hook
 *
 * Helper hook to handle action bar item click events.
 */
export function useActionBarAction() {
  const { dispatch } = useLayout();

  const handleAction = (action: Action) => {
    // Dispatch action event that can be handled by parent components
    if (action.onClick) {
      action.onClick();
    } else {
      // Dispatch a custom event for global handling
      const event = new CustomEvent('actionbar:action', {
        detail: { actionId: action.id, action: action.action },
      });
      window.dispatchEvent(event);
    }

    // If action has a context change, update the layout context
    if (action.contextChange) {
      dispatch({ type: 'SET_CONTEXT', payload: action.contextChange });
    }
  };

  return { handleAction };
}
