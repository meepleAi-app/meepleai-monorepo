'use client';

/**
 * CardTagStrip - Tag strip rendering
 *
 * Wraps the existing TagStrip and VerticalTagStack components with
 * variant-specific visibility and sizing logic.
 *
 * @module components/ui/data-display/meeple-card/parts/CardTagStrip
 */

import React from 'react';

import { TagStrip } from '../../meeple-card-features/TagStrip';
import { VerticalTagStack } from '../../meeple-card-parts';

import type {
  MeepleCardProps,
  MeepleCardVariant,
  MeepleEntityType,
  TagConfig,
  TagPresetKey,
} from '../types';

export interface CardTagStripProps {
  /** Card layout variant */
  variant: MeepleCardVariant;
  /** Entity type */
  entity: MeepleEntityType;
  /** Custom entity color */
  customColor?: string;
  /** Tags for vertical tag strip */
  tags?: (TagPresetKey | TagConfig)[];
  /** Max visible tags before overflow */
  maxVisibleTags?: number;
  /** Force show tag strip */
  showTagStrip?: boolean;
  /** Status badge value(s) for VerticalTagStack */
  status?: MeepleCardProps['status'];
  /** Whether to show status icon in VerticalTagStack */
  showStatusIcon?: boolean;
  /** Badge text for VerticalTagStack */
  badge?: string;
}

/**
 * Renders both the vertical tag strip (left edge) and the
 * vertical tag stack (entity badge + status + custom badge) for grid/featured.
 */
export function CardTagStrip({
  variant,
  entity,
  customColor,
  tags,
  maxVisibleTags = 3,
  showTagStrip,
  status,
  showStatusIcon,
  badge,
}: CardTagStripProps) {
  return (
    <>
      {/* Vertical Tag Strip (left-edge tag display) */}
      {(showTagStrip || (tags && tags.length > 0)) && (
        <TagStrip
          tags={tags || []}
          maxVisible={maxVisibleTags}
          variant={
            variant === 'grid' || variant === 'featured'
              ? 'desktop'
              : variant === 'list'
                ? 'tablet'
                : 'mobile'
          }
        />
      )}

      {/* Vertical tag stack: entity badge + status + custom badge (grid/featured only) */}
      {(variant === 'grid' || variant === 'featured') && (
        <VerticalTagStack
          entity={entity}
          customColor={customColor}
          status={status}
          showStatusIcon={showStatusIcon}
          badge={badge}
        />
      )}
    </>
  );
}
