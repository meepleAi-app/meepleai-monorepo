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
  /** Action label (for tooltip) */
  label: string;
  /** Click handler */
  onClick: () => void;
  /** Disabled state */
  disabled?: boolean;
  /** Hide action (conditional visibility) */
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

  // eslint-disable-next-line security/detect-object-injection
  const entityColor = customColor || entityColors[entityType].hsl;

  const buttonSize = size === 'sm' ? 'w-[30px] h-[30px]' : 'w-[36px] h-[36px]';
  const iconSize = size === 'sm' ? 'w-[15px] h-[15px]' : 'w-[18px] h-[18px]';

  return (
    <>
      {visibleActions.map((action, index) => {
        const Icon = action.icon;

        // Staggered animation delays (reverse order: leftmost appears last)
        // For 3 actions: 80ms, 40ms, 0ms
        const delay = (visibleActions.length - 1 - index) * 40;

        return (
          <button
            key={index}
            onClick={(e) => {
              e.stopPropagation();
              action.onClick();
            }}
            disabled={action.disabled}
            className={cn(
              // Base styles
              buttonSize,
              'rounded-full flex items-center justify-center',
              'border border-white/50',
              'bg-white/80 backdrop-blur-[8px]',
              'transition-all duration-300 ease-out',
              // Hover state
              'hover:bg-white hover:scale-110',
              'hover:shadow-md',
              // Focus state
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-1',
              // Disabled state
              'disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100',
              // Fade in on parent card hover
              'opacity-0 pointer-events-none',
              'group-hover:opacity-100 group-hover:pointer-events-auto',
            )}
            style={{
              transitionDelay: `${delay}ms`,
              // Entity-colored focus ring
              ['--tw-ring-color' as string]: `hsl(${entityColor})`,
            }}
            aria-label={action.label}
            data-tooltip={action.label}
          >
            <Icon
              className={cn(
                iconSize,
                'stroke-slate-600 transition-colors duration-200',
              )}
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
        );
      })}
    </>
  );
});

// Tooltip styles (inject into document head if not already present)
if (typeof document !== 'undefined' && !document.getElementById('meeple-qa-tooltip-styles')) {
  const style = document.createElement('style');
  style.id = 'meeple-qa-tooltip-styles';
  style.textContent = `
    [data-tooltip] {
      position: relative;
    }
    [data-tooltip]::after {
      content: attr(data-tooltip);
      position: absolute;
      bottom: calc(100% + 6px);
      left: 50%;
      transform: translateX(-50%) translateY(-4px);
      background: hsl(var(--foreground));
      color: hsl(var(--background));
      font-size: 11px;
      padding: 4px 8px;
      border-radius: 6px;
      white-space: nowrap;
      pointer-events: none;
      opacity: 0;
      transition: opacity 0.15s ease, transform 0.15s ease;
      z-index: 50;
    }
    [data-tooltip]:hover::after {
      opacity: 1;
      transform: translateX(-50%) translateY(0);
    }
  `;
  document.head.appendChild(style);
}
