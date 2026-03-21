'use client';

/**
 * CardCover - Cover image wrapper with 4-corner overlay system
 *
 * Wraps CoverImage with CoverOverlay for the 4-corner slot layout:
 *   - top-left: label stack (coverLabels)
 *   - top-right: entity type mana pip (showEntityType)
 *   - bottom-left: subtype icons (subtypeIcons) or legacy mechanicIcon
 *   - bottom-right: state badge (stateLabel)
 *
 * @module components/ui/data-display/meeple-card/parts/CardCover
 */

import React from 'react';

import { CoverOverlay } from './CoverOverlay';
import { CoverImage } from '../../meeple-card-parts';

import type { CoverLabel, SubtypeIcon, MeepleEntityType, MeepleCardVariant } from '../types';

export interface CardCoverProps {
  /** Image source URL */
  src?: string;
  /** Alt text for the image */
  alt: string;
  /** Card layout variant */
  variant: MeepleCardVariant;
  /** Entity type for color scheme and placeholder */
  entity: MeepleEntityType;
  /** Custom entity color (HSL format) */
  customColor?: string;
  /** Whether the shimmer effect is active (hover state) */
  showShimmer?: boolean;
  /** Additional CSS class */
  className?: string;
  /** Top-left: label stack */
  coverLabels?: CoverLabel[];
  /** Top-right: show entity type mana pip */
  showEntityType?: boolean;
  /** Bottom-left: subtype classification icons */
  subtypeIcons?: SubtypeIcon[];
  /** @deprecated Use subtypeIcons instead. Maps to subtypeIcons[0]. */
  mechanicIcon?: React.ReactNode;
  /** Bottom-right: state badge */
  stateLabel?: {
    text: string;
    variant: 'success' | 'warning' | 'error' | 'info';
  };
  /** Action strip rendered inside cover's relative wrapper (cover-relative positioning) */
  actionStrip?: React.ReactNode;
}

/**
 * Cover image section with 4-corner overlay system.
 * Delegates actual image rendering to the existing CoverImage component.
 * Compact variant does not render a cover image.
 */
export function CardCover({
  src,
  alt,
  variant,
  entity,
  customColor,
  className: _className,
  coverLabels,
  showEntityType,
  subtypeIcons,
  mechanicIcon,
  stateLabel,
  actionStrip,
}: CardCoverProps) {
  if (variant === 'compact') return null;

  return (
    <div className="relative">
      <CoverImage src={src} alt={alt} variant={variant} entity={entity} customColor={customColor} />
      <CoverOverlay
        entity={entity}
        customColor={customColor}
        coverLabels={coverLabels}
        showEntityType={showEntityType}
        subtypeIcons={subtypeIcons}
        mechanicIcon={mechanicIcon}
        stateLabel={stateLabel}
      />
      {actionStrip}
    </div>
  );
}
