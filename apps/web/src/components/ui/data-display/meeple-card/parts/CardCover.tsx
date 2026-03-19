'use client';

/**
 * CardCover - Cover image wrapper with optional MtG-inspired overlay slots
 *
 * Wraps the existing CoverImage component from meeple-card-parts with
 * variant-specific shimmer animation overlay and aspect ratio logic.
 *
 * Overlay slots (MtG anatomy):
 *   - mechanicIcon: bottom-left — classification icon (frosted glass pill)
 *   - stateLabel:   bottom-right — semantic state badge (success/warning/error/info)
 *
 * @module components/ui/data-display/meeple-card/parts/CardCover
 */

import React from 'react';

import { cn } from '@/lib/utils';

import { CoverImage } from '../../meeple-card-parts';
import { coverOverlayStyles } from '../../meeple-card-styles';

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
  /** MtG-inspired overlay: classification icon (bottom-left of cover image) */
  mechanicIcon?: React.ReactNode;
  /** MtG-inspired overlay: state badge (bottom-right of cover image) */
  stateLabel?: {
    text: string;
    variant: 'success' | 'warning' | 'error' | 'info';
  };
}

/**
 * Cover image section with optional MtG-inspired overlay slots.
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
  mechanicIcon,
  stateLabel,
}: CardCoverProps) {
  if (variant === 'compact') return null;

  const hasOverlay = mechanicIcon || stateLabel;

  return (
    <div className="relative">
      <CoverImage src={src} alt={alt} variant={variant} entity={entity} customColor={customColor} />
      {hasOverlay && (
        <div className={coverOverlayStyles.container}>
          {mechanicIcon ? (
            <div
              data-testid="mechanic-icon-slot"
              data-slot="mechanic-icon"
              className={coverOverlayStyles.mechanicIcon}
            >
              {mechanicIcon}
            </div>
          ) : (
            <div />
          )}
          {stateLabel && (
            <div
              data-testid="state-label-slot"
              data-slot="state-label"
              className={cn(
                coverOverlayStyles.stateLabel.base,
                coverOverlayStyles.stateLabel[stateLabel.variant]
              )}
            >
              {stateLabel.text}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
