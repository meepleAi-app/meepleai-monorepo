/**
 * MeepleCardInfoButton - Always-visible navigation or drawer trigger
 *
 * Glass morphism circular button positioned as rightmost element in top-actions row.
 * Supports two modes:
 *   - Button mode (onClick): opens ExtraMeepleCardDrawer (Issue #5025)
 *   - Link mode (href): navigates to entity detail page (legacy)
 *
 * @module components/ui/data-display/meeple-card-info-button
 * @see Issue #4030 - MeepleCard Multi-Entity System
 * @see Issue #5025 - MeepleCard drawer button
 */

import React from 'react';

import { Info } from 'lucide-react';
import Link from 'next/link';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

import { entityColors } from './meeple-card';

import type { MeepleEntityType } from './meeple-card';

// ============================================================================
// Types
// ============================================================================

export interface MeepleCardInfoButtonProps {
  /**
   * Click handler — button mode (Issue #5025).
   * When provided, renders as <button> that opens the drawer.
   * Takes precedence over href.
   */
  onClick?: () => void;
  /**
   * Navigation href — legacy link mode.
   * Used as fallback when onClick is not provided.
   * @deprecated Prefer entityId + drawer (Issue #5025)
   */
  href?: string;
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
// Shared style helpers
// ============================================================================

function buildButtonStyles(
  size: 'sm' | 'md',
  entityColor: string,
): {
  buttonSize: string;
  iconSize: string;
  commonClass: string;
  style: React.CSSProperties;
} {
  const buttonSize = size === 'sm'
    ? 'w-11 h-11 md:w-[30px] md:h-[30px]'
    : 'w-11 h-11 md:w-[36px] md:h-[36px]';
  const iconSize = size === 'sm'
    ? 'w-5 h-5 md:w-[14px] md:h-[14px]'
    : 'w-5 h-5 md:w-[16px] md:h-[16px]';
  const commonClass = cn(
    buttonSize,
    'relative rounded-full flex items-center justify-center flex-shrink-0',
    'border border-white/50',
    'bg-white/80 backdrop-blur-[8px]',
    'transition-all duration-300 ease-out',
    'hover:bg-white hover:scale-110',
    'hover:shadow-md',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-1',
    'opacity-100',
  );
  const style = {
    ['--tw-ring-color' as string]: `hsl(${entityColor})`,
  } as React.CSSProperties;

  return { buttonSize, iconSize, commonClass, style };
}

// ============================================================================
// Component
// ============================================================================

export const MeepleCardInfoButton = React.memo(function MeepleCardInfoButton({
  onClick,
  href,
  entityType,
  customColor,
  tooltip = 'View details',
  size = 'sm',
}: MeepleCardInfoButtonProps) {
  // eslint-disable-next-line security/detect-object-injection
  const entityColor = customColor || entityColors[entityType].hsl;
  const { iconSize, commonClass, style } = buildButtonStyles(size, entityColor);

  const glowSpan = (
    <span
      className="absolute inset-0 rounded-full opacity-0 hover:opacity-100 transition-opacity duration-200 pointer-events-none"
      style={{ boxShadow: `0 0 0 2px hsl(${entityColor})` }}
      aria-hidden="true"
    />
  );

  const inner = (
    <>
      <Info
        className={cn(iconSize, 'stroke-slate-600 transition-colors duration-200')}
        strokeWidth={2}
      />
      {glowSpan}
    </>
  );

  return (
    <TooltipProvider delayDuration={200}>
      <Tooltip>
        <TooltipTrigger asChild>
          {onClick ? (
            /* Button mode — opens ExtraMeepleCardDrawer (Issue #5025) */
            <button
              type="button"
              onClick={(e) => { e.stopPropagation(); onClick(); }}
              className={commonClass}
              style={style}
              aria-label={tooltip}
              data-testid="meeple-card-info-button"
            >
              {inner}
            </button>
          ) : (
            /* Link mode — legacy navigation */
            <Link
              href={href ?? '#'}
              onClick={(e) => e.stopPropagation()}
              className={commonClass}
              style={style}
              aria-label={tooltip}
              data-testid="meeple-card-info-button"
            >
              {inner}
            </Link>
          )}
        </TooltipTrigger>
        <TooltipContent side="bottom" sideOffset={10}>
          {tooltip}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
});
