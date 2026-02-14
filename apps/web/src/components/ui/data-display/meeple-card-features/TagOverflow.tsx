/**
 * TagOverflow - Overflow Counter Badge for Tag Strip
 * Issue #4181 - Vertical Tag Component
 *
 * Displays "+N" badge when tags exceed maxVisible limit.
 * Circular badge with responsive sizing matching tag strip variants.
 */

'use client';

import React from 'react';

import { cva, type VariantProps } from 'class-variance-authority';

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

// ============================================================================
// CVA Variants
// ============================================================================

const tagOverflowVariants = cva(
  'inline-flex items-center justify-center font-bold rounded-full bg-muted text-muted-foreground transition-all duration-200 select-none',
  {
    variants: {
      variant: {
        // Desktop: 32px diameter
        desktop: 'w-8 h-8 text-xs',
        // Tablet: 28px diameter
        tablet: 'w-7 h-7 text-[10px]',
        // Mobile: 24px diameter
        mobile: 'w-6 h-6 text-[9px]',
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

export interface TagOverflowProps extends VariantProps<typeof tagOverflowVariants> {
  /** Number of overflow tags */
  count: number;
  /** Custom className */
  className?: string;
  /** Accessibility label */
  'aria-label'?: string;
}

// ============================================================================
// Component
// ============================================================================

export const TagOverflow = React.memo(function TagOverflow({
  count,
  variant = 'desktop',
  className,
  'aria-label': ariaLabel,
}: TagOverflowProps) {
  if (count <= 0) return null;

  const formattedCount = count > 99 ? '99+' : `+${count}`;
  const description = count === 1 ? '1 altro tag nascosto' : `${count} altri tag nascosti`;

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <span
            className={cn(tagOverflowVariants({ variant }), className)}
            data-testid="tag-overflow"
            aria-label={ariaLabel || description}
          >
            {formattedCount}
          </span>
        </TooltipTrigger>
        <TooltipContent side="right" align="center">
          <p className="text-xs">{description}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
});

TagOverflow.displayName = 'TagOverflow';
