'use client';

/**
 * CardCover - Cover image wrapper with shimmer effect
 *
 * Wraps the existing CoverImage component from meeple-card-parts with
 * variant-specific shimmer animation overlay and aspect ratio logic.
 *
 * @module components/ui/data-display/meeple-card/parts/CardCover
 */

import React from 'react';

import { CoverImage } from '../../meeple-card-parts';

import type { MeepleEntityType, MeepleCardVariant } from '../types';

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
}

/**
 * Cover image section with shimmer animation overlay.
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
}: CardCoverProps) {
  if (variant === 'compact') return null;

  return (
    <CoverImage src={src} alt={alt} variant={variant} entity={entity} customColor={customColor} />
  );
}
