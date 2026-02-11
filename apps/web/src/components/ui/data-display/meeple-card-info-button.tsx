/**
 * MeepleCardInfoButton - Always-visible navigation to entity detail page
 *
 * Glass morphism circular button positioned as rightmost element in top-actions row.
 * Links to entity detail page with entity-colored border glow on hover.
 *
 * @module components/ui/data-display/meeple-card-info-button
 * @see Issue #4030 - MeepleCard Multi-Entity System
 */

import React from 'react';

import { Info } from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';

import { entityColors } from './meeple-card';

import type { MeepleEntityType } from './meeple-card';

// ============================================================================
// Types
// ============================================================================

export interface MeepleCardInfoButtonProps {
  /** Navigation href (entity detail page) */
  href: string;
  /** Entity type for hover glow color */
  entityType: MeepleEntityType;
  /** Custom entity color override */
  customColor?: string;
  /** Tooltip text (default: "View details") */
  tooltip?: string;
  /** Size variant */
  size?: 'sm' | 'md';
}

// ============================================================================
// Component
// ============================================================================

export const MeepleCardInfoButton = React.memo(function MeepleCardInfoButton({
  href,
  entityType,
  customColor,
  tooltip = 'View details',
  size = 'sm',
}: MeepleCardInfoButtonProps) {
  // eslint-disable-next-line security/detect-object-injection
  const entityColor = customColor || entityColors[entityType].hsl;

  const buttonSize = size === 'sm' ? 'w-[30px] h-[30px]' : 'w-[36px] h-[36px]';
  const iconSize = size === 'sm' ? 'w-[14px] h-[14px]' : 'w-[16px] h-[16px]';

  return (
    <TooltipProvider delayDuration={200}>
      <Tooltip>
        <TooltipTrigger asChild>
          <Link
            href={href}
            onClick={(e) => e.stopPropagation()}
            className={cn(
              // Base styles
              buttonSize,
              'rounded-full flex items-center justify-center flex-shrink-0',
              'border border-white/50',
              'bg-white/80 backdrop-blur-[8px]',
              'transition-all duration-300 ease-out',
              // Hover state
              'hover:bg-white hover:scale-110',
              'hover:shadow-md',
              // Focus state
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-1',
              // Always visible (no opacity animation like QuickActions)
              'opacity-100',
            )}
            style={{
              // Entity-colored focus ring
              ['--tw-ring-color' as string]: `hsl(${entityColor})`,
            }}
            aria-label={tooltip}
            data-testid="meeple-card-info-button"
          >
            <Info
              className={cn(
                iconSize,
                'stroke-slate-600 transition-colors duration-200',
              )}
              strokeWidth={2}
            />

            {/* Entity-colored hover border glow */}
            <span
              className="absolute inset-0 rounded-full opacity-0 hover:opacity-100 transition-opacity duration-200 pointer-events-none"
              style={{
                boxShadow: `0 0 0 2px hsl(${entityColor})`,
              }}
              aria-hidden="true"
            />
          </Link>
        </TooltipTrigger>
        <TooltipContent side="bottom" sideOffset={10}>
          {tooltip}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
});
