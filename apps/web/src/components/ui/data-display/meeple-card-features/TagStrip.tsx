/**
 * TagStrip - Vertical Tag Container for MeepleCard
 * Issue #4181 - Vertical Tag Component
 *
 * Left-edge vertical strip displaying max 3 tags + overflow counter.
 * Positioned along the left border with full card height.
 * Z-index: above image, below hover overlay.
 */

'use client';

import React from 'react';

import { cva, type VariantProps } from 'class-variance-authority';

import { cn } from '@/lib/utils';

import { type TagConfig, type TagPresetKey } from './tag-presets';
import { TagBadge } from './TagBadge';
import { TagOverflow } from './TagOverflow';

// ============================================================================
// CVA Variants
// ============================================================================

const tagStripVariants = cva(
  'absolute left-0 top-0 bottom-0 flex flex-col items-center gap-1 py-2 z-20',
  {
    variants: {
      variant: {
        // Desktop: 32px width
        desktop: 'w-8',
        // Tablet: 28px width
        tablet: 'w-7',
        // Mobile: 24px width
        mobile: 'w-6',
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

export interface TagStripProps extends VariantProps<typeof tagStripVariants> {
  /** Array of tags to display (preset keys or custom configs) */
  tags: (TagPresetKey | TagConfig)[];
  /** Maximum number of visible tags (default: 3) */
  maxVisible?: number;
  /** Custom className */
  className?: string;
  /** Accessibility label */
  'aria-label'?: string;
}

// ============================================================================
// Component
// ============================================================================

export const TagStrip = React.memo(function TagStrip({
  tags,
  maxVisible = 3,
  variant = 'desktop',
  className,
  'aria-label': ariaLabel,
}: TagStripProps) {
  if (!tags || tags.length === 0) return null;

  const visibleTags = tags.slice(0, maxVisible);
  const overflowCount = Math.max(0, tags.length - maxVisible);
  const iconOnly = variant === 'mobile';

  return (
    <div
      className={cn(tagStripVariants({ variant }), className)}
      data-testid="tag-strip"
      aria-label={ariaLabel || `${tags.length} tag${tags.length !== 1 ? 's' : ''}`}
      role="list"
    >
      {/* Visible tags */}
      {visibleTags.map((tag, index) => {
        const key = typeof tag === 'string' ? tag : tag.key;
        return (
          <div key={`${key}-${index}`} role="listitem">
            <TagBadge
              tag={tag}
              variant={variant}
              iconOnly={iconOnly}
              showIcon={true}
            />
          </div>
        );
      })}

      {/* Overflow counter */}
      {overflowCount > 0 && (
        <div role="listitem">
          <TagOverflow count={overflowCount} variant={variant} />
        </div>
      )}
    </div>
  );
});

TagStrip.displayName = 'TagStrip';
