/**
 * MeepleCardQuickActions - Hover-reveal action buttons
 *
 * Glass morphism circular buttons that fade in on card hover with staggered animation.
 * Positioned in top-right corner alongside InfoButton.
 *
 * @module components/ui/data-display/meeple-card-quick-actions
 * @see Issue #4030 - MeepleCard Multi-Entity System
 */

import React from 'react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

import { entityColors } from './meeple-card';

import type { MeepleEntityType } from './meeple-card';
import type { LucideIcon } from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

export interface QuickAction {
  /** Lucide icon component */
  icon: LucideIcon;
  /** Action label (for tooltip on enabled state) */
  label: string;
  /** Click handler */
  onClick: () => void;
  /** Disabled state - button visible but not clickable */
  disabled?: boolean;
  /**
   * Tooltip shown when button is disabled.
   * Required when disabled=true to explain why the action is unavailable.
   * Example: "Login to add to library"
   */
  disabledTooltip?: string;
  /** Hide action entirely (action not applicable in this context/state) */
  hidden?: boolean;
}

export interface MeepleCardQuickActionsProps {
  /** Array of quick actions (max 3 recommended) */
  actions: QuickAction[];
  /** Entity type for hover glow color */
  entityType: MeepleEntityType;
  /** Custom entity color override */
  customColor?: string;
  /** Size variant */
  size?: 'sm' | 'md';
}

// ============================================================================
// Component
// ============================================================================

export const MeepleCardQuickActions = React.memo(function MeepleCardQuickActions({
  actions,
  entityType,
  customColor,
  size = 'sm',
}: MeepleCardQuickActionsProps) {
  // Filter out hidden actions
  const visibleActions = actions.filter(action => !action.hidden);

  if (visibleActions.length === 0) {
    return null;
  }

  const entityColor = customColor || entityColors[entityType].hsl;

  // Mobile: 44px touch targets (WCAG), Desktop: compact sizes
  const buttonSize =
    size === 'sm' ? 'w-11 h-11 md:w-[30px] md:h-[30px]' : 'w-11 h-11 md:w-[36px] md:h-[36px]';
  const iconSize =
    size === 'sm' ? 'w-5 h-5 md:w-[15px] md:h-[15px]' : 'w-5 h-5 md:w-[18px] md:h-[18px]';

  return (
    <TooltipProvider delayDuration={200}>
      {visibleActions.map((action, index) => {
        const Icon = action.icon;

        // Staggered animation delays (reverse order: leftmost appears last)
        // For 3 actions: 80ms, 40ms, 0ms
        const delay = (visibleActions.length - 1 - index) * 40;

        return (
          <Tooltip key={index}>
            <TooltipTrigger asChild>
              {/*
               * Use aria-disabled instead of native disabled so that pointer events
               * (mouseenter/focus) still fire and Radix Tooltip can open.
               * This is required to show disabledTooltip on disabled actions.
               * See: https://www.radix-ui.com/primitives/docs/components/tooltip#with-disabled-button
               */}
              <button
                onClick={e => {
                  e.stopPropagation();
                  if (!action.disabled) {
                    action.onClick();
                  }
                }}
                aria-disabled={action.disabled}
                className={cn(
                  // Base styles
                  buttonSize,
                  'rounded-full flex items-center justify-center',
                  'border border-white/50',
                  'bg-white/80 backdrop-blur-[8px]',
                  'transition-all duration-300 ease-out',
                  // Hover state (suppressed when aria-disabled)
                  !action.disabled && 'hover:bg-white hover:scale-110',
                  !action.disabled && 'hover:shadow-md',
                  // Focus state
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-1',
                  // Aria-disabled styling (mimics native disabled)
                  action.disabled && 'opacity-50 cursor-not-allowed',
                  // Mobile: always visible. Desktop: fade in on card hover
                  'md:opacity-0 md:pointer-events-none',
                  'md:group-hover:opacity-100 md:group-hover:pointer-events-auto'
                )}
                style={{
                  transitionDelay: `${delay}ms`,
                  // Entity-colored focus ring
                  ['--tw-ring-color' as string]: `hsl(${entityColor})`,
                }}
                aria-label={
                  action.disabled && action.disabledTooltip ? action.disabledTooltip : action.label
                }
              >
                <Icon
                  className={cn(iconSize, 'stroke-slate-600 transition-colors duration-200')}
                  strokeWidth={2}
                  style={{
                    // Entity-colored icon on hover
                    ['--hover-color' as string]: `hsl(${entityColor})`,
                  }}
                />

                {/* Entity-colored hover glow */}
                <span
                  className="absolute inset-0 rounded-full opacity-0 hover:opacity-100 transition-opacity duration-200 pointer-events-none"
                  style={{
                    boxShadow: `0 0 0 2px hsl(${entityColor})`,
                  }}
                  aria-hidden="true"
                />
              </button>
            </TooltipTrigger>
            <TooltipContent side="bottom" sideOffset={10}>
              {action.disabled && action.disabledTooltip ? action.disabledTooltip : action.label}
            </TooltipContent>
          </Tooltip>
        );
      })}
    </TooltipProvider>
  );
});
