'use client';

/**
 * CardBadges - Status badges, rating display, and entity link badges
 *
 * Composes StatusBadge, RatingDisplay, and EntityLinkBadge into a
 * variant-aware badge rendering layer.
 *
 * @module components/ui/data-display/meeple-card/parts/CardBadges
 */

import React from 'react';

import { cn } from '@/lib/utils';

import { EntityLinkBadge } from '../../entity-link/entity-link-badge';
import { StatusBadge } from '../../meeple-card-features/StatusBadge';
import { RatingDisplay } from '../../meeple-card-parts';

import type { MeepleCardProps, MeepleCardVariant } from '../types';

export interface CardBadgesProps {
  /** Card layout variant */
  variant: MeepleCardVariant;
  /** Status badge value(s) */
  status?: MeepleCardProps['status'];
  /** Whether to show status icon */
  showStatusIcon?: boolean;
  /** Rating value */
  rating?: number;
  /** Rating max scale (default 5) */
  ratingMax?: number;
  /** Entity link count for badge overlay */
  linkCount?: number;
  /** Callback when entity link badge is clicked */
  onLinksClick?: () => void;
  /** Badge text overlay for non-grid/non-featured variants */
  badge?: string;
}

/**
 * Renders status badges, rating display, and entity link badges
 * based on the current variant.
 */
export function CardBadges({
  variant,
  status,
  showStatusIcon,
  rating,
  ratingMax = 5,
  linkCount,
  onLinksClick,
  badge,
}: CardBadgesProps) {
  const isHeroOrFeatured = variant === 'hero' || variant === 'featured';

  return (
    <>
      {/* EntityLink badge -- top-right corner (grid/featured only) */}
      {linkCount !== undefined &&
        linkCount > 0 &&
        (variant === 'grid' || variant === 'featured') && (
          <EntityLinkBadge count={linkCount} onClick={onLinksClick} />
        )}

      {/* Status Badge for non-grid/non-featured/non-list variants */}
      {status && variant !== 'grid' && variant !== 'featured' && variant !== 'list' && (
        <StatusBadge
          status={status}
          showIcon={showStatusIcon}
          className="absolute top-12 left-4 z-11"
        />
      )}

      {/* Rating (non-compact) */}
      {rating !== undefined && variant !== 'compact' && (
        <RatingDisplay
          rating={rating}
          max={ratingMax}
          className={cn('mb-2', variant === 'hero' && 'text-white')}
        />
      )}

      {/* Badge overlay for hero/compact (not grid/featured/list where VerticalTagStack handles it) */}
      {badge &&
        variant !== 'grid' &&
        variant !== 'featured' &&
        variant !== 'list' &&
        !isHeroOrFeatured && (
          <span className="absolute top-3 right-3 bg-card/90 backdrop-blur-sm px-2 py-0.5 rounded-md text-xs font-semibold text-muted-foreground border border-border/50">
            {badge}
          </span>
        )}
    </>
  );
}
