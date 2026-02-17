/**
 * TagBadge - Single Tag Display Component for Vertical Tag Strip
 * Issue #4181 - Vertical Tag Component
 *
 * Responsive tag badge with icon support and icon-only mode for mobile.
 * Integrates with tag-presets.ts for entity-specific configurations.
 */

'use client';

import React from 'react';

import { cva, type VariantProps } from 'class-variance-authority';

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

import { getTagPreset, type TagConfig, type TagPresetKey } from './tag-presets';

// ============================================================================
// CVA Variants
// ============================================================================

const tagBadgeVariants = cva(
  'inline-flex items-center justify-center gap-1 font-semibold transition-all duration-200',
  {
    variants: {
      variant: {
        // Desktop: full width (32px), full text labels
        desktop: 'w-8 px-1.5 py-1 text-[10px] leading-none rounded-md',
        // Tablet: medium width (28px), abbreviated text
        tablet: 'w-7 px-1 py-0.5 text-[9px] leading-none rounded-md',
        // Mobile: small width (24px), icon-only mode
        mobile: 'w-6 p-1 text-[8px] leading-none rounded-full',
      },
    },
    defaultVariants: {
      variant: 'desktop',
    },
  }
);

// ============================================================================
// Types
// ============================================================================

export interface TagBadgeProps extends VariantProps<typeof tagBadgeVariants> {
  /** Tag preset key or custom tag config */
  tag: TagPresetKey | TagConfig;
  /** Icon-only mode (hides label, shows only icon) */
  iconOnly?: boolean;
  /** Show icon alongside label */
  showIcon?: boolean;
  /** Enable pulse animation (for "new" or featured tags) */
  animated?: boolean;
  /** Custom className */
  className?: string;
  /** Accessibility label */
  'aria-label'?: string;
}

// ============================================================================
// Component
// ============================================================================

export const TagBadge = React.memo(function TagBadge({
  tag,
  variant = 'desktop',
  iconOnly = false,
  showIcon = true,
  animated = false,
  className,
  'aria-label': ariaLabel,
}: TagBadgeProps) {
  // Resolve tag configuration
  const config: TagConfig | undefined =
    typeof tag === 'string' ? getTagPreset(tag) : (tag as TagConfig);

  if (!config) {
    console.warn(`TagBadge: Unknown tag preset "${tag}"`);
    return null;
  }

  const { label, abbr, bgClass, textClass, icon: Icon, description } = config;

  // Determine label based on variant and iconOnly mode
  const displayLabel =
    variant === 'mobile' || iconOnly ? '' : variant === 'tablet' ? abbr : label;

  // Icon size based on variant
  const iconSize = variant === 'mobile' ? 'w-3 h-3' : variant === 'tablet' ? 'w-2.5 h-2.5' : 'w-3 h-3';

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <span
            className={cn(
              tagBadgeVariants({ variant }),
              bgClass,
              textClass,
              'select-none',
              animated && 'animate-mc-badge-pulse',
              className
            )}
            data-testid={`tag-badge-${config.key}`}
            aria-label={ariaLabel || description}
          >
            {showIcon && Icon && <Icon className={cn(iconSize, 'flex-shrink-0')} aria-hidden="true" />}
            {displayLabel && <span className="truncate">{displayLabel}</span>}
          </span>
        </TooltipTrigger>
        <TooltipContent side="right" align="center">
          <p className="text-xs">{description}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
});

TagBadge.displayName = 'TagBadge';
