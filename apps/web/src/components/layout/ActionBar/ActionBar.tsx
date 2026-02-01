/**
 * ActionBar Component
 * Issue #3290 - Phase 4: ActionBar System
 *
 * Bottom action bar with context-aware actions, priority system, and overflow menu.
 * Features:
 * - Responsive slots (3 mobile, 4 tablet, 6 desktop)
 * - Priority-based action visibility
 * - Overflow menu for extra actions
 * - Stagger animation on context switch
 * - Empty state handling
 */

'use client';

import { forwardRef, type ComponentPropsWithoutRef } from 'react';

import { useLayout } from '@/components/layout/LayoutProvider';
import { useActionBar, useActionBarAction } from '@/hooks/useActionBar';
import { cn } from '@/lib/utils';

import { ActionBarItem } from './ActionBarItem';
import { OverflowMenu } from './OverflowMenu';

export interface ActionBarProps
  extends Omit<ComponentPropsWithoutRef<'div'>, 'children'> {
  /** Display variant for action items */
  variant?: 'icon-only' | 'icon-label';
  /** Animation stagger delay between items (in ms) */
  staggerDelay?: number;
}

/**
 * ActionBar Component
 *
 * Main action bar container with responsive slots and overflow handling.
 */
export const ActionBar = forwardRef<HTMLDivElement, ActionBarProps>(
  function ActionBar(
    { variant = 'icon-only', staggerDelay = 50, className, ...props },
    ref
  ) {
    const { responsive } = useLayout();
    const { isMobile } = responsive;
    const { visibleActions, overflowActions, isEmpty, isVisible } = useActionBar();
    const { handleAction } = useActionBarAction();

    // Don't render if not visible or empty
    if (!isVisible || isEmpty) {
      return null;
    }

    return (
      <div
        ref={ref}
        className={cn(
          // Fixed positioning at bottom
          'fixed bottom-0 left-0 right-0 z-40',

          // Height with safe area
          'h-14',
          'pb-safe-area-inset-bottom',

          // Glass morphism effect
          'bg-background/95 dark:bg-card/95',
          'backdrop-blur-lg',
          'border-t border-border/50',

          // Shadow for depth
          'shadow-[0_-2px_10px_rgba(0,0,0,0.1)]',
          'dark:shadow-[0_-2px_10px_rgba(0,0,0,0.3)]',

          className
        )}
        role="toolbar"
        aria-label="Azioni rapide"
        {...props}
      >
        <div
          className={cn(
            'h-full max-w-screen-lg mx-auto',
            'flex items-center justify-center gap-2',
            'px-4',
            isMobile && 'justify-between'
          )}
        >
          {/* Visible actions */}
          <div className="flex items-center gap-2">
            {visibleActions.map((action, index) => (
              <ActionBarItem
                key={action.id}
                action={action}
                variant={variant}
                onClick={handleAction}
                animationDelay={index * staggerDelay}
              />
            ))}
          </div>

          {/* Overflow menu */}
          {overflowActions.length > 0 && (
            <OverflowMenu
              actions={overflowActions}
              onActionSelect={handleAction}
              animationDelay={visibleActions.length * staggerDelay}
            />
          )}
        </div>
      </div>
    );
  }
);

ActionBar.displayName = 'ActionBar';

/**
 * ActionBarSpacer Component
 *
 * A spacer component to prevent content from being hidden behind the ActionBar.
 * Add this at the bottom of your page content.
 */
export function ActionBarSpacer({ className }: { className?: string }) {
  const { isVisible, isEmpty } = useActionBar();

  if (!isVisible || isEmpty) {
    return null;
  }

  return (
    <div
      className={cn(
        'h-14',
        'pb-safe-area-inset-bottom',
        className
      )}
      aria-hidden="true"
    />
  );
}
