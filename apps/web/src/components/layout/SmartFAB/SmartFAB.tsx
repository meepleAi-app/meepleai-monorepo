/**
 * SmartFAB Component
 * Issue #3291 - Phase 5: Smart FAB
 *
 * Smart Floating Action Button that changes icon/action based on context,
 * with long-press quick menu.
 *
 * Features:
 * - Context-aware primary action
 * - Long-press to reveal quick menu
 * - Hide on keyboard/modal/fast-scroll
 * - Morph animation between contexts
 * - Mobile only
 */

'use client';

import { forwardRef, type ComponentPropsWithoutRef } from 'react';

import {
  Plus,
  Play,
  MessageSquare,
  PlusCircle,
  type LucideIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { FAB_POSITION } from '@/config/fab';
import { useFAB } from '@/hooks/useFAB';
import { useLongPress } from '@/hooks/useLongPress';
import { cn } from '@/lib/utils';

import { QuickMenu } from './QuickMenu';

/**
 * Icon mapping for FAB icons
 */
const ICON_MAP: Record<string, LucideIcon> = {
  plus: Plus,
  play: Play,
  'message-square': MessageSquare,
  'plus-circle': PlusCircle,
};

export interface SmartFABProps
  extends Omit<ComponentPropsWithoutRef<'button'>, 'children' | 'onClick'> {
  /** Override visibility */
  forceVisible?: boolean;
}

/**
 * SmartFAB Component
 *
 * Context-aware floating action button with long-press quick menu.
 * Only visible on mobile devices.
 */
export const SmartFAB = forwardRef<HTMLButtonElement, SmartFABProps>(
  function SmartFAB({ forceVisible, className, ...props }, ref) {
    const {
      isVisible,
      config,
      isQuickMenuOpen,
      openQuickMenu,
      closeQuickMenu,
      triggerAction,
      triggerQuickAction,
    } = useFAB();

    // Long press handlers for quick menu
    const longPressHandlers = useLongPress(openQuickMenu, {
      onClick: triggerAction,
    });

    // Determine final visibility
    const shouldShow = forceVisible ?? isVisible;

    // Don't render if not visible or no config
    if (!shouldShow || !config) {
      return null;
    }

    // Get icon component
    const IconComponent = ICON_MAP[config.icon] ?? Plus;

    return (
      <div
        className={cn(
          'fixed z-50',
          'transition-all duration-200'
        )}
        style={{
          right: `${FAB_POSITION.right}px`,
          bottom: `${FAB_POSITION.bottom}px`,
        }}
      >
        {/* Quick Menu */}
        <QuickMenu
          items={config.quickMenuItems}
          isOpen={isQuickMenuOpen}
          onClose={closeQuickMenu}
          onSelect={triggerQuickAction}
        />

        {/* FAB Button */}
        <Button
          ref={ref}
          variant="default"
          size="icon"
          className={cn(
            // Size
            'h-14 w-14',
            'rounded-full',

            // Shadow
            'shadow-lg hover:shadow-xl',
            'dark:shadow-primary/20',

            // Animation
            'animate-in fade-in-0 zoom-in-95',
            'transition-all duration-200',
            'hover:scale-105 active:scale-95',

            // Quick menu open state
            isQuickMenuOpen && 'rotate-45',

            className
          )}
          aria-label={config.label}
          aria-expanded={isQuickMenuOpen}
          aria-haspopup="menu"
          {...longPressHandlers}
          {...props}
        >
          <IconComponent
            className={cn(
              'h-6 w-6',
              'transition-transform duration-200',
              isQuickMenuOpen && 'rotate-45'
            )}
            aria-hidden="true"
          />
        </Button>
      </div>
    );
  }
);

SmartFAB.displayName = 'SmartFAB';
